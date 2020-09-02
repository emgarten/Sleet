using System.Threading.Tasks;

namespace Sleet
{
    /// <summary>
    /// Provides svg version badges.
    /// </summary>
    public class Badges : ISleetService
    {
        private readonly SleetContext _context;

        public string Name { get; } = nameof(Badges);

        public Badges(SleetContext context)
        {
            _context = context;
        }

        public async Task ApplyOperationsAsync(SleetOperations operations)
        {
            var before = await operations.OriginalIndex.Packages.GetPackagesAsync();
            var after = await operations.UpdatedIndex.Packages.GetPackagesAsync();

            await BadgeUtility.UpdateBadges(_context, before, after);
        }

        public Task PreLoadAsync(SleetOperations operations)
        {
            // Noop
            return Task.FromResult(true);
        }
    }
}