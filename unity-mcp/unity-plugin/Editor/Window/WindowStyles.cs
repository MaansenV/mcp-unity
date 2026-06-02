namespace UnityMCP.Editor.Window
{
    /// <summary>
    /// USS class name constants. Centralizes style references to avoid
    /// magic strings and make refactoring safe.
    /// </summary>
    internal static class WindowStyles
    {
        // Window structure
        public const string Root = "root";
        public const string Header = "header";
        public const string HeaderTitle = "header-title";
        public const string HeaderStatus = "header-status";
        public const string Tabs = "tabs";
        public const string Tab = "tab";
        public const string TabActive = "active";
        public const string Content = "content";

        // Status pill
        public const string StatusPill = "status-pill";
        public const string StatusDot = "status-dot";
        public const string StatusText = "status-text";
        public const string StatusConnected = "connected";
        public const string StatusConnecting = "connecting";
        public const string StatusError = "error";
        public const string StatusDisconnected = "disconnected";

        // Card / Section
        public const string Card = "card";
        public const string CardHeader = "card-header";
        public const string CardTitle = "card-title";
        public const string CardSubtitle = "card-subtitle";
        public const string CardCount = "card-count";

        // Key-Value row
        public const string KvRow = "kv-row";
        public const string KvLabel = "kv-label";
        public const string KvValue = "kv-value";
        public const string KvValueMono = "mono";
        public const string KvValueAccent = "accent";
        public const string KvValueSuccess = "success";
        public const string KvValueWarning = "warning";
        public const string KvValueError = "error";

        // Buttons
        public const string Actions = "actions";
        public const string BtnPrimary = "primary";
        public const string BtnFullWidth = "full-width";

        // Step (Setup tab)
        public const string Step = "step";
        public const string StepIcon = "step-icon";
        public const string StepIconDone = "done";
        public const string StepIconPending = "pending";
        public const string StepIconRunning = "running";
        public const string StepContent = "step-content";
        public const string StepTitle = "step-title";
        public const string StepTitleDone = "done";
        public const string StepDetail = "step-detail";
        public const string StepAction = "step-action";
        public const string Divider = "divider";

        // Filter
        public const string FilterBar = "filter-bar";
        public const string Chip = "chip";
        public const string ChipActive = "active";
        public const string Search = "search";

        // List
        public const string List = "list";
        public const string ListRow = "list-row";
        public const string ListRowSelected = "selected";
        public const string ListRowTitle = "list-row-title";
        public const string ListRowMeta = "list-row-meta";

        // Empty state
        public const string Empty = "empty";
        public const string EmptyText = "empty-text";

        // Client
        public const string Client = "client";
        public const string ClientInfo = "client-info";
        public const string ClientName = "client-name";
        public const string ClientAddress = "client-address";
        public const string ClientMeta = "client-meta";

        // Log
        public const string LogEntry = "log-entry";
        public const string LogTime = "log-time";
        public const string LogCat = "log-cat";
        public const string LogInfo = "log-info";
        public const string LogWarn = "log-warn";
        public const string LogError = "log-error";
        public const string LogDebug = "log-debug";

        // Code block
        public const string CodeBlock = "code-block";

        // Hero status (Connection tab top)
        public const string HeroStatus = "hero-status";
        public const string HeroStatusDot = "hero-status-dot";
        public const string HeroStatusText = "hero-status-text";

        // Status card (green-bordered wireframe accent on the Status tab)
        public const string StatusCard = "status-card";
    }
}
