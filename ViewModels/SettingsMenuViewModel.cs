using System;

namespace JimmiLauncher.ViewModels
{
    public partial class SettingsMenuViewModel : MenuViewModelBase
    {
        public override bool CanNavigateReplays { get => false; protected set => throw new Exception("Cannot set CanNavigateReplays in SettingsMenuViewModel"); }
        public override bool CanNavigateMain { get => true; protected set => throw new Exception("Cannot set CanNavigateMain in SettingsMenuViewModel"); }
        public override bool CanNavigateOnline { get => false; protected set => throw new Exception("Cannot set CanNavigateOnline in SettingsMenuViewModel"); }
        public override bool CanNavigateOffline { get => false; protected set => throw new Exception("Cannot set CanNavigateOffline in SettingsMenuViewModel"); }

        private Action<string>? _onNavigateRequested;

        public SettingsMenuViewModel(Action<string>? onNavigateRequested = null)
        {
            _onNavigateRequested = onNavigateRequested;
        }
    }
}