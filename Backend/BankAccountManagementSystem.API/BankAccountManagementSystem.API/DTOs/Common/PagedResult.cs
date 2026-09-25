namespace BankAccountManagementSystem.API.DTOs.Common
{
    /// <summary>
    /// Generic pagination wrapper for API responses.
    /// Provides consistent structure for paged lists across the application.
    /// </summary>
    /// <typeparam name="T">Type of item contained in the page.</typeparam>
    public class PagedResult<T>
    {
        /// <summary>
        /// The list of items on the current page.
        /// </summary>
        public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();

        /// <summary>
        /// Current page number (1-based).
        /// </summary>
        public int Page { get; set; }

        /// <summary>
        /// Number of items requested per page.
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Total count of matching items across all pages.
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Total number of pages calculated from TotalCount and PageSize.
        /// </summary>
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    }
}
