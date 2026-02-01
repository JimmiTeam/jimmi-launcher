namespace JimmiLauncher.ViewModels
{
    /// <summary>
    /// An abstract class for enabling page navigation.
    /// </summary>
    public abstract class MenuViewModelBase : ViewModelBase
    {
        public abstract bool CanNavigateReplays { get; protected set; }
        public abstract bool CanNavigateMain { get; protected set; }
        public abstract bool CanNavigateOnline { get; protected set; }
        public abstract bool CanNavigateOffline { get; protected set; }
    }
}