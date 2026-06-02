using System.Threading;
using UnityEngine.UIElements;
using UnityMCP.Editor.Core;
using UnityMCP.Editor.Logging;
using UnityMCP.Editor.Services;
using UnityMCP.Editor.Settings;

namespace UnityMCP.Editor.Window
{
    internal class SetupTabController : TabController
    {
        private readonly McpSettings _settings;
        private readonly McpLogBuffer _logBuffer;
        private readonly McpRuntimeState _runtimeState;

        // Step UI refs
        private Label _goIcon;
        private Label _goTitle;
        private Label _goDetail;
        private Label _serverIcon;
        private Label _serverTitle;
        private Label _serverDetail;
        private Label _opencodeIcon;
        private Label _opencodeTitle;
        private Label _opencodeDetail;
        private Button _setupButton;
        private ProgressBar _progressBar;
        private Label _progressLabel;

        public SetupTabController(McpSettings settings, McpLogBuffer logBuffer, McpRuntimeState runtimeState)
        {
            _settings = settings;
            _logBuffer = logBuffer;
            _runtimeState = runtimeState;
        }

        public override void Build(VisualElement container)
        {
            container.Add(BuildPrerequisitesCard());
            container.Add(BuildSetupCard());
        }

        // --- Prerequisites ---

        private VisualElement BuildPrerequisitesCard()
        {
            var card = Card("Prerequisites", "Required tools and configuration");

            // Go
            var goStep = BuildStepRow(out _goIcon, out _goTitle, out _goDetail,
                onAction: CheckGo, actionText: "Check", titleText: "Go Installation");
            card.Add(goStep);

            // Server Binary
            var serverStep = BuildStepRow(out _serverIcon, out _serverTitle, out _serverDetail,
                onAction: BuildServer, actionText: "Build", titleText: "Server Binary");
            card.Add(serverStep);

            // OpenCode
            var opencodeStep = BuildStepRow(out _opencodeIcon, out _opencodeTitle, out _opencodeDetail,
                onAction: ConfigureOpenCode, actionText: "Configure", titleText: "OpenCode Config");
            card.Add(opencodeStep);

            // Path preview
            var pathPreview = new Label($"Target: {OpencodeConfigService.GetGlobalConfigPath()}");
            pathPreview.AddToClassList(WindowStyles.CodeBlock);
            card.Add(pathPreview);

            return card;
        }

        // --- One-Click Setup ---

        private VisualElement BuildSetupCard()
        {
            var card = Card("One-Click Setup", "Automated setup of all prerequisites");

            _progressBar = new ProgressBar { value = 0, lowValue = 0, highValue = 100 };
            card.Add(_progressBar);

            _progressLabel = new Label("Ready to start");
            _progressLabel.AddToClassList(WindowStyles.KvLabel);
            card.Add(_progressLabel);

            _setupButton = new Button(RunOneClickSetup) { text = "Run One-Click Setup" };
            _setupButton.AddToClassList(WindowStyles.BtnPrimary);
            _setupButton.AddToClassList(WindowStyles.BtnFullWidth);
            card.Add(_setupButton);

            return card;
        }

        // --- Step builder ---

        private VisualElement BuildStepRow(
            out Label icon, out Label title, out Label detail,
            System.Action onAction, string actionText, string titleText)
        {
            var step = new VisualElement();
            step.AddToClassList(WindowStyles.Step);

            icon = new Label("•");
            icon.AddToClassList(WindowStyles.StepIcon);
            icon.AddToClassList(WindowStyles.StepIconPending);
            step.Add(icon);

            var content = new VisualElement();
            content.AddToClassList(WindowStyles.StepContent);

            title = new Label(titleText);
            title.AddToClassList(WindowStyles.StepTitle);
            content.Add(title);

            detail = new Label("Not checked");
            detail.AddToClassList(WindowStyles.StepDetail);
            content.Add(detail);

            step.Add(content);

            var action = new Button(onAction) { text = actionText };
            action.AddToClassList(WindowStyles.StepAction);
            step.Add(action);

            return step;
        }

        // --- Step actions ---

        private async void CheckGo()
        {
            SetStepState(_goIcon, _goTitle, _goDetail, StepState.Running, "Checking...");
            var result = await GoInstallationService.CheckGoAsync();

            if (result.IsInstalled)
            {
                SetStepState(_goIcon, _goTitle, _goDetail, StepState.Done, result.Version);
                _logBuffer.Add(McpLogLevel.Info, McpLogCategory.Setup, $"Go found: {result.Version}");
            }
            else
            {
                SetStepState(_goIcon, _goTitle, _goDetail, StepState.Pending, "Not found");
                _logBuffer.Add(McpLogLevel.Error, McpLogCategory.Setup, $"Go not found: {result.Error}");
            }
        }

        private async void BuildServer()
        {
            SetStepState(_serverIcon, _serverTitle, _serverDetail, StepState.Running, "Building...");
            var result = await ServerBuildService.BuildAsync(_settings,
                new System.Progress<string>(msg => _logBuffer.Add(McpLogLevel.Info, McpLogCategory.Setup, msg)));

            if (result.Success)
            {
                SetStepState(_serverIcon, _serverTitle, _serverDetail, StepState.Done, "Built successfully");
            }
            else
            {
                SetStepState(_serverIcon, _serverTitle, _serverDetail, StepState.Pending, "Build failed");
                _logBuffer.Add(McpLogLevel.Error, McpLogCategory.Setup, result.Error);
            }
        }

        private async void ConfigureOpenCode()
        {
            SetStepState(_opencodeIcon, _opencodeTitle, _opencodeDetail, StepState.Running, "Configuring...");
            var result = await OpencodeConfigService.ConfigureGlobalAsync(_settings);

            if (result.Success)
            {
                SetStepState(_opencodeIcon, _opencodeTitle, _opencodeDetail, StepState.Done, "Configured");
                _logBuffer.Add(McpLogLevel.Info, McpLogCategory.Setup, $"OpenCode config updated: {result.Path}");
            }
            else
            {
                SetStepState(_opencodeIcon, _opencodeTitle, _opencodeDetail, StepState.Pending, "Failed");
                _logBuffer.Add(McpLogLevel.Error, McpLogCategory.Setup, result.Error);
            }
        }

        private async void RunOneClickSetup()
        {
            _setupButton.SetEnabled(false);
            _setupButton.text = "Running...";
            _progressBar.value = 0;
            _progressLabel.text = "Starting...";
            _runtimeState.SetSetupProgress(0, "Starting setup...");

            var (ocConfigured, _) = OpencodeConfigService.CheckGlobalConfig();
            SetStepState(_opencodeIcon, _opencodeTitle, _opencodeDetail,
                ocConfigured ? StepState.Done : StepState.Pending,
                ocConfigured ? "Configured" : "Not configured");

            var cts = new CancellationTokenSource();
            var result = await UnityMcpSetupService.RunOneClickSetupAsync(
                _settings,
                _logBuffer,
                (progress, operation) =>
                {
                    _progressBar.value = progress * 100f;
                    _progressLabel.text = operation;
                    _runtimeState.SetSetupProgress(progress, operation);
                },
                cts.Token);

            _progressLabel.text = result.Success ? "Setup complete" : $"Setup failed: {result.Error}";

            if (result.Success)
                _logBuffer.Add(McpLogLevel.Info, McpLogCategory.Setup, "One-click setup completed");
            else
                _logBuffer.Add(McpLogLevel.Error, McpLogCategory.Setup, result.Error);

            _setupButton.SetEnabled(true);
            _setupButton.text = "Run One-Click Setup";
        }

        // --- Step state helper ---

        private enum StepState { Pending, Running, Done }

        private static void SetStepState(Label icon, Label title, Label detail, StepState state, string detailText)
        {
            if (icon != null)
            {
                icon.RemoveFromClassList(WindowStyles.StepIconPending);
                icon.RemoveFromClassList(WindowStyles.StepIconRunning);
                icon.RemoveFromClassList(WindowStyles.StepIconDone);
                icon.text = state switch
                {
                    StepState.Done => "✓",
                    StepState.Running => "●",
                    _ => "•",
                };
                var iconClass = state switch
                {
                    StepState.Done => WindowStyles.StepIconDone,
                    StepState.Running => WindowStyles.StepIconRunning,
                    _ => WindowStyles.StepIconPending,
                };
                icon.AddToClassList(iconClass);
            }

            if (title != null)
            {
                title.RemoveFromClassList(WindowStyles.StepTitleDone);
                if (state == StepState.Done)
                    title.AddToClassList(WindowStyles.StepTitleDone);
            }

            if (detail != null)
                detail.text = detailText;
        }
    }
}
