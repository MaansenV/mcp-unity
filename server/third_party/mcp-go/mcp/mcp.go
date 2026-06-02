package mcp

import (
	"encoding/json"
)

// Tool describes an MCP tool.
type Tool struct {
	Name        string          `json:"name"`
	Description string          `json:"description,omitempty"`
	InputSchema json.RawMessage `json:"inputSchema,omitempty"`
}

type toolConfig struct {
	name        string
	description string
	properties  map[string]any
	required    []string
}

// ToolOption configures a tool definition.
type ToolOption func(*toolConfig)

// ParamOption configures a tool parameter.
type ParamOption func(*paramConfig)

type paramConfig struct {
	description string
	required    bool
}

// NewTool creates a tool with typed parameters.
func NewTool(name string, opts ...ToolOption) Tool {
	cfg := toolConfig{
		name:       name,
		properties: map[string]any{},
	}
	for _, opt := range opts {
		opt(&cfg)
	}

	schema := map[string]any{
		"type":                 "object",
		"properties":           cfg.properties,
		"additionalProperties": false,
	}
	if len(cfg.required) > 0 {
		schema["required"] = cfg.required
	}
	raw, _ := json.Marshal(schema)
	return Tool{Name: cfg.name, Description: cfg.description, InputSchema: raw}
}

// WithDescription sets the tool description.
func WithDescription(desc string) ToolOption {
	return func(cfg *toolConfig) {
		cfg.description = desc
	}
}

// WithString adds a string parameter.
func WithString(name string, opts ...ParamOption) ToolOption {
	return func(cfg *toolConfig) {
		pc := paramConfig{}
		for _, opt := range opts {
			opt(&pc)
		}
		cfg.properties[name] = paramSchema("string", pc.description)
		if pc.required {
			cfg.required = append(cfg.required, name)
		}
	}
}

// WithNumber adds a numeric parameter.
func WithNumber(name string, opts ...ParamOption) ToolOption {
	return func(cfg *toolConfig) {
		pc := paramConfig{}
		for _, opt := range opts {
			opt(&pc)
		}
		cfg.properties[name] = paramSchema("number", pc.description)
		if pc.required {
			cfg.required = append(cfg.required, name)
		}
	}
}

// WithBoolean adds a boolean parameter.
func WithBoolean(name string, opts ...ParamOption) ToolOption {
	return func(cfg *toolConfig) {
		pc := paramConfig{}
		for _, opt := range opts {
			opt(&pc)
		}
		cfg.properties[name] = paramSchema("boolean", pc.description)
		if pc.required {
			cfg.required = append(cfg.required, name)
		}
	}
}

// WithObject adds an object parameter.
func WithObject(name string, opts ...ParamOption) ToolOption {
	return func(cfg *toolConfig) {
		pc := paramConfig{}
		for _, opt := range opts {
			opt(&pc)
		}
		cfg.properties[name] = paramSchema("object", pc.description)
		if pc.required {
			cfg.required = append(cfg.required, name)
		}
	}
}

// Required marks a parameter as required.
func Required() ParamOption {
	return func(cfg *paramConfig) {
		cfg.required = true
	}
}

// Description describes a parameter.
func Description(desc string) ParamOption {
	return func(cfg *paramConfig) {
		cfg.description = desc
	}
}

func paramSchema(typ, description string) map[string]any {
	s := map[string]any{"type": typ}
	if description != "" {
		s["description"] = description
	}
	return s
}

// TextContent is a simple content item.
type TextContent struct {
	Type string `json:"type"`
	Text string `json:"text"`
}

// NewTextContent constructs a text content item.
func NewTextContent(text string) TextContent { return TextContent{Type: "text", Text: text} }

// CallToolRequest is the minimal call tool request payload.
type CallToolRequest struct {
	Name      string         `json:"name"`
	Arguments map[string]any `json:"arguments,omitempty"`
}

// CallToolResult is the minimal call tool result payload.
type CallToolResult struct {
	Content []any `json:"content,omitempty"`
	IsError bool  `json:"isError,omitempty"`
}

// NewToolResultText returns a text-only result.
func NewToolResultText(text string) *CallToolResult {
	return &CallToolResult{Content: []any{NewTextContent(text)}}
}
