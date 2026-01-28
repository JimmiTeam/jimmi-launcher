using System;

namespace JimmiLauncher.ViewModels
{
    /// <summary>
    /// An abstract class for enabling page navigation.
    /// </summary>
    public partial class ReplayMenuViewModel : MenuViewModelBase
    {
        public override bool CanNavigateReplays {
            get => false;
            protected set => throw new Exception("Cannot set CanNavigateReplays in ReplayMenuViewModel");
        }

        public override bool CanNavigateMain { 
            get => true;
            protected set => throw new Exception("Cannot set CanNavigateMain in ReplayMenuViewModel");
        }
    }
}