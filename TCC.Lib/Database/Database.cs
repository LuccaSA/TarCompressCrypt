using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nito.AsyncEx;
using TCC.Lib.Helpers;

namespace TCC.Lib.Database
{
    public class Database
    {  
        private readonly AsyncLazy<TccBackupDbContext> _lazyBackup;
        private readonly AsyncLazy<TccRestoreDbContext> _lazyRestore;

        public Database(TccBackupDbContext tccBackupDbContext, TccRestoreDbContext tccRestoreDbContext, IOptions<TccSettings> options)
        {  
            _lazyBackup = new AsyncLazy<TccBackupDbContext>(async () =>
            {
                if (options.Value.Provider == Provider.InMemory)
                {
                    await tccBackupDbContext.Database.EnsureCreatedAsync();
                }
                else
                {
                    await tccBackupDbContext.Database.MigrateAsync();
                }
                return tccBackupDbContext;
            });

            _lazyRestore = new AsyncLazy<TccRestoreDbContext>(async () =>
            {
                if (options.Value.Provider == Provider.InMemory)
                {
                    await tccRestoreDbContext.Database.EnsureCreatedAsync();
                }
                else
                {
                    await tccRestoreDbContext.Database.MigrateAsync();
                }
                return tccRestoreDbContext;
            });
        }

        public async Task<TccBackupDbContext> BackupDbAsync() => await _lazyBackup;
        public async Task<TccRestoreDbContext> RestoreDbAsync() => await _lazyRestore;
    }
}