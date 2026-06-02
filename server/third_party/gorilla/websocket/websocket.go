package websocket

import (
	"bufio"
	"crypto/sha1"
	"encoding/base64"
	"encoding/binary"
	"errors"
	"fmt"
	"io"
	"net"
	"net/http"
	"net/url"
	"strconv"
	"strings"
	"sync"
	"time"
)

// Message types compatible with gorilla/websocket.
const (
	TextMessage   = 1
	BinaryMessage = 2
	CloseMessage  = 8
	CloseNormalClosure = 1000
	PingMessage   = 9
	PongMessage   = 10
)

// Conn is a minimal websocket connection implementation supporting text, ping, pong, and close frames.
type Conn struct {
	mu          sync.Mutex
	conn        net.Conn
	br          *bufio.Reader
	remote      net.Addr
	closed      bool
	readLimit   int64
	pongHandler func(string) error
}

// Upgrader upgrades HTTP connections to websocket connections.
type Upgrader struct {
	CheckOrigin func(*http.Request) bool
}

// Upgrade performs a websocket handshake and returns a Conn.
func (u Upgrader) Upgrade(w http.ResponseWriter, r *http.Request, responseHeader http.Header) (*Conn, error) {
	if u.CheckOrigin != nil && !u.CheckOrigin(r) {
		return nil, errors.New("websocket origin rejected")
	}
	if !strings.EqualFold(r.Header.Get("Connection"), "Upgrade") && !strings.Contains(strings.ToLower(r.Header.Get("Connection")), "upgrade") {
		return nil, errors.New("missing upgrade connection header")
	}
	if !strings.EqualFold(r.Header.Get("Upgrade"), "websocket") {
		return nil, errors.New("missing websocket upgrade header")
	}
	hj, ok := w.(http.Hijacker)
	if !ok {
		return nil, errors.New("response writer does not support hijacking")
	}
	conn, brw, err := hj.Hijack()
	if err != nil {
		return nil, err
	}

	key := strings.TrimSpace(r.Header.Get("Sec-WebSocket-Key"))
	if key == "" {
		_ = conn.Close()
		return nil, errors.New("missing Sec-WebSocket-Key")
	}
	accept := computeAcceptKey(key)

	headers := http.Header{}
	headers.Set("Upgrade", "websocket")
	headers.Set("Connection", "Upgrade")
	headers.Set("Sec-WebSocket-Accept", accept)
	for k, vals := range responseHeader {
		for _, v := range vals {
			headers.Add(k, v)
		}
	}

	response := "HTTP/1.1 101 Switching Protocols\r\n"
	for k, vals := range headers {
		for _, v := range vals {
			response += k + ": " + v + "\r\n"
		}
	}
	response += "\r\n"
	if _, err := brw.WriteString(response); err != nil {
		_ = conn.Close()
		return nil, err
	}
	if err := brw.Flush(); err != nil {
		_ = conn.Close()
		return nil, err
	}

	return &Conn{conn: conn, br: bufio.NewReader(conn), remote: conn.RemoteAddr()}, nil
}

// ReadMessage reads a single websocket data message.
func (c *Conn) ReadMessage() (messageType int, p []byte, err error) {
	for {
		frameType, payload, err := c.readFrame()
		if err != nil {
			return 0, nil, err
		}
		switch frameType {
		case TextMessage, BinaryMessage:
			return int(frameType), payload, nil
		case PingMessage:
			_ = c.WriteMessage(PongMessage, payload)
		case PongMessage:
			if c.pongHandler != nil {
				_ = c.pongHandler(string(payload))
			}
		case CloseMessage:
			_ = c.Close()
			return 0, nil, io.EOF
		default:
			// ignore unsupported control frames
		}
	}
}

// WriteMessage writes a websocket data/control frame.
func (c *Conn) WriteMessage(messageType int, data []byte) error {
	c.mu.Lock()
	defer c.mu.Unlock()
	if c.closed {
		return io.ErrClosedPipe
	}
	return c.writeFrame(byte(messageType), data)
}

// Close closes the websocket connection.
func (c *Conn) Close() error {
	c.mu.Lock()
	defer c.mu.Unlock()
	if c.closed {
		return nil
	}
	c.closed = true
	return c.conn.Close()
}

// SetReadDeadline sets the read deadline.
func (c *Conn) SetReadDeadline(t time.Time) error { return c.conn.SetReadDeadline(t) }

// SetWriteDeadline sets the write deadline.
func (c *Conn) SetWriteDeadline(t time.Time) error { return c.conn.SetWriteDeadline(t) }

// SetReadLimit sets the maximum frame payload size.
func (c *Conn) SetReadLimit(limit int64) { c.readLimit = limit }

// SetPongHandler sets the pong handler.
func (c *Conn) SetPongHandler(h func(string) error) { c.pongHandler = h }

// RemoteAddr returns the remote address.
func (c *Conn) RemoteAddr() net.Addr { return c.remote }

func (c *Conn) readFrame() (byte, []byte, error) {
	head, err := c.br.ReadByte()
	if err != nil {
		return 0, nil, err
	}
	fin := head&0x80 != 0
	opcode := head & 0x0F
	_ = fin

	second, err := c.br.ReadByte()
	if err != nil {
		return 0, nil, err
	}
	masked := second&0x80 != 0
	length := int64(second & 0x7F)
	switch length {
	case 126:
		var ext uint16
		if err := binary.Read(c.br, binary.BigEndian, &ext); err != nil {
			return 0, nil, err
		}
		length = int64(ext)
	case 127:
		var ext uint64
		if err := binary.Read(c.br, binary.BigEndian, &ext); err != nil {
			return 0, nil, err
		}
		length = int64(ext)
	}
	if c.readLimit > 0 && length > c.readLimit {
		return 0, nil, fmt.Errorf("websocket message too large: %d", length)
	}
	var maskKey [4]byte
	if masked {
		if _, err := io.ReadFull(c.br, maskKey[:]); err != nil {
			return 0, nil, err
		}
	}
	payload := make([]byte, length)
	if _, err := io.ReadFull(c.br, payload); err != nil {
		return 0, nil, err
	}
	if masked {
		for i := range payload {
			payload[i] ^= maskKey[i%4]
		}
	}
	return opcode, payload, nil
}

func (c *Conn) writeFrame(opcode byte, payload []byte) error {
	var hdr [10]byte
	hdr[0] = 0x80 | (opcode & 0x0F)
	length := len(payload)
	idx := 2
	switch {
	case length < 126:
		hdr[1] = byte(length)
	case length <= 0xFFFF:
		hdr[1] = 126
		binary.BigEndian.PutUint16(hdr[2:4], uint16(length))
		idx = 4
	default:
		hdr[1] = 127
		binary.BigEndian.PutUint64(hdr[2:10], uint64(length))
		idx = 10
	}
	if _, err := c.conn.Write(hdr[:idx]); err != nil {
		return err
	}
	if len(payload) > 0 {
		_, err := c.conn.Write(payload)
		return err
	}
	return nil
}

func computeAcceptKey(key string) string {
	const magic = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"
	h := sha1.Sum([]byte(key + magic))
	return base64.StdEncoding.EncodeToString(h[:])
}

// Helper for tests or logs.
func (c *Conn) String() string {
	if c.remote == nil {
		return "<nil>"
	}
	return strconv.Quote(c.remote.String())
}

// URLFromRequest returns the websocket URL that the client should connect to.
func URLFromRequest(r *http.Request, path string) string {
	scheme := "ws"
	if r.TLS != nil {
		scheme = "wss"
	}
	u := url.URL{Scheme: scheme, Host: r.Host, Path: path}
	return u.String()
}

// FormatCloseMessage formats a close code and reason into a close message payload.
func FormatCloseMessage(code int, text string) []byte {
	if code == CloseNormalClosure && text == "" {
		return nil
	}
	buf := make([]byte, 2+len(text))
	binary.BigEndian.PutUint16(buf, uint16(code))
	copy(buf[2:], text)
	return buf
}
