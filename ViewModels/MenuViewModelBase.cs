namespace JimmiLauncher.ViewModels
{
    /// <summary>
    /// An abstract class for enabling page navigation.
    /// </summary>
    public abstract class MenuViewModelBase : ViewModelBase
    {
        /// <summary>
        /// Gets if the user can navigate to the next page
        /// </summary>
        public abstract bool CanNavigateReplays { get; protected set; }

        /// <summary>
        /// Gets if the user can navigate to the previous page
        /// </summary>
        public abstract bool CanNavigateMain { get; protected set; }
    }
}