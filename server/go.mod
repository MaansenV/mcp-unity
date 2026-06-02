module github.com/unity-mcp/server

go 1.23.0

require (
	github.com/mark3labs/mcp-go v0.54.1
	github.com/gorilla/websocket v1.5.3
)

replace github.com/mark3labs/mcp-go => ./third_party/mcp-go
replace github.com/gorilla/websocket => ./third_party/gorilla/websocket
