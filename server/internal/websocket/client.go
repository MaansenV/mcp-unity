package websocket

import (
	"encoding/json"
	"fmt"
	"net/http"
	"sync"
	"sync/atomic"
	"time"

	gw "github.com/gorilla/websocket"
)

const clientSendBufferSize = 64

// Client represents one Unity websocket connection.
type Client struct {
	ID         string
	RemoteAddr string
	hub        *Hub
	conn       *gw.Conn
	request    *http.Request
	lastSeen   atomic.Int64 // UnixNano, thread-safe
	closed     chan struct{}
	closeMu    sync.Mutex
	closedOnce bool
	send       chan []byte
}

// NewClient creates a websocket client wrapper.
func NewClient(hub *Hub, conn *gw.Conn, req *http.Request) *Client {
	c := &Client{
		ID:         fmt.Sprintf("unity-%d", time.Now().UnixNano()),
		RemoteAddr: req.RemoteAddr,
		hub:        hub,
		conn:       conn,
		request:    req,
		closed:     make(chan struct{}),
		send:       make(chan []byte, clientSendBufferSize),
	}
	c.lastSeen.Store(time.Now().UnixNano())
	return c
}

func (c *Client) IsAlive() bool {
	select {
	case <-c.closed:
		return false
	default:
		return true
	}
}

func (c *Client) LastSeen() time.Time {
	return time.Unix(0, c.lastSeen.Load())
}

func (c *Client) run() {
	defer func() {
		c.hub.unregisterClient(c)
		_ = c.Close()
	}()

	c.conn.SetReadLimit(1 << 20)
	_ = c.conn.SetReadDeadline(time.Now().Add(60 * time.Second))
	c.conn.SetPongHandler(func(string) error {
		_ = c.conn.SetReadDeadline(time.Now().Add(60 * time.Second))
		c.touch()
		return nil
	})

	go c.writePump()
	c.readPump()
}

func (c *Client) readPump() {
	for {
		_, data, err := c.conn.ReadMessage()
		if err != nil {
			return
		}
		c.touch()

		var resp responseEnvelope
		if err := json.Unmarshal(data, &resp); err != nil {
			c.hub.logger.Printf("[DEBUG-MCP] invalid Unity message from %s: %v payload=%s", c.ID, err, string(data))
			continue
		}
		if resp.ID == "" {
			c.hub.logger.Printf("[DEBUG-MCP] Unity message without id from %s payload=%s", c.ID, string(data))
			continue
		}
		c.hub.logger.Printf("[DEBUG-MCP] Unity response received from %s id=%s payload=%s", c.ID, resp.ID, string(data))
		c.hub.handleResponse(resp)
	}
}

// writePump is the SOLE writer to the websocket connection.
// All messages (ping, text, close) go through the send channel.
func (c *Client) writePump() {
	ticker := time.NewTicker(c.hub.heartbeatInterval)
	defer ticker.Stop()
	defer func() {
		// Send close frame before closing
		_ = c.conn.SetWriteDeadline(time.Now().Add(1 * time.Second))
		_ = c.conn.WriteMessage(gw.CloseMessage,
			gw.FormatCloseMessage(gw.CloseNormalClosure, ""))
		_ = c.conn.Close()
		c.markClosed()
	}()

	for {
		select {
		case payload := <-c.send:
			_ = c.conn.SetWriteDeadline(time.Now().Add(10 * time.Second))
			if err := c.conn.WriteMessage(gw.TextMessage, payload); err != nil {
				return
			}
		case <-ticker.C:
			_ = c.conn.SetWriteDeadline(time.Now().Add(10 * time.Second))
			if err := c.conn.WriteMessage(gw.PingMessage, nil); err != nil {
				return
			}
		case <-c.closed:
			return
		}
	}
}

// Send queues a message to be sent by writePump (thread-safe).
func (c *Client) Send(payload []byte) error {
	if !c.IsAlive() {
		return fmt.Errorf("client is closed")
	}

	select {
	case c.send <- payload:
		return nil
	case <-c.closed:
		return fmt.Errorf("client is closed")
	default:
		return fmt.Errorf("client send buffer is full")
	}
}

// Close closes the websocket connection.
func (c *Client) Close() error {
	c.markClosed()
	_ = c.conn.SetWriteDeadline(time.Now().Add(1 * time.Second))
	return c.conn.Close()
}

func (c *Client) touch() {
	c.lastSeen.Store(time.Now().UnixNano())
}

func (c *Client) markClosed() {
	c.closeMu.Lock()
	if !c.closedOnce {
		close(c.closed)
		c.closedOnce = true
	}
	c.closeMu.Unlock()
}
