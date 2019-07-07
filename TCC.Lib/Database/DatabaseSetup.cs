using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TCC.Lib.Helpers;
using TCC.Lib.Options;

namespace TCC.Lib.Database
{
    public class DatabaseSetup
    {
        private readonly TccBackupDbContext _tccBackupDbContext;
        private readonly TccRestoreDbContext _tccRestoreDbContext;
        private readonly IOptions<TccSettings> _options;

        public DatabaseSetup(TccBackupDbContext tccBackupDbContext, TccRestoreDbContext tccRestoreDbContext, IOptions<TccSettings> options)
        {
            _tccBackupDbContext = tccBackupDbContext;
            _tccRestoreDbContext = tccRestoreDbContext;
            _options = options;
        }

        public async Task EnsureDatabaseExistsAsync(Mode commandMode)
        {
            if (_options.Value.Provider == Provider.InMemory)
            {
                if (commandMode.HasFlag(Mode.Compress))
                {
                    await _tccBackupDbContext.Database.EnsureCreatedAsync();
                }

                if (commandMode.HasFlag(Mode.Decompress))
                {
                    await _tccRestoreDbContext.Database.EnsureCreatedAsync();
                }
            }
            else
            {
                if (commandMode.HasFlag(Mode.Compress))
                {
                    if ((await _tccBackupDbContext.Database.GetPendingMigrationsAsync()).Any())
                    {
                        await _tccBackupDbContext.Database.MigrateAsync();
                    }
                }

                if (commandMode.HasFlag(Mode.Decompress))
                {
                    if ((await _tccRestoreDbContext.Database.GetPendingMigrationsAsync()).Any())
                    {
                        await _tccRestoreDbContext.Database.MigrateAsync();
                    }
                }
            }
        }
    }
}