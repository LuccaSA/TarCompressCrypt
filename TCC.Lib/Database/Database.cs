using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nito.AsyncEx;
using TCC.Lib.Helpers;

namespace TCC.Lib.Database
{
    public class Database
    {  
        private readonly AsyncLazy<TccBackupDbContext> _lazy;

        public Database(TccBackupDbContext tccDbContext, IOptions<TccSettings> options)
        {  
            _lazy = new AsyncLazy<TccBackupDbContext>(async () =>
            {
                if (options.Value.Provider == Provider.InMemory)
                {
                    await tccDbContext.Database.EnsureCreatedAsync();
                }
                else
                {
                    await tccDbContext.Database.MigrateAsync();
                }
                return tccDbContext;
            });
        }

        public async Task<TccBackupDbContext> GetDbAsync() => await _lazy;
    }
}