using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.LoggerDB.Context;
using Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.LoggerDB.Entity;
using Academikus.AgenteInteligenteMentoresTareas.Entity.Common;
using System.Transactions;

namespace Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.LoggerDB
{
    public class LoggerDbRepository : ILoggerDbRepository
    {
        private readonly ConnectionString _connectionString;
        protected readonly ILogger<LoggerDbRepository> _logger;

        public LoggerDbRepository(IOptions<ConnectionString> connectionString, ILogger<LoggerDbRepository> logger)
        {
            _connectionString = connectionString.Value;
            _logger = logger;
        }

        public List<LogItem> GetLogs()
        {
            List<LogItem> result = new List<LogItem>();
            using (new TransactionScope(TransactionScopeOption.RequiresNew, new TransactionOptions() { IsolationLevel = IsolationLevel.ReadUncommitted }))
            {
                using (var dbContext = new LoggerDBContext(_connectionString.LoggerDBEntities))
                {
                    result = dbContext.LogItems.Where(x => x.LogItemLevel == "WARN").ToList();
                }
            }
            return result;
        }
        public async Task InsertLog(LogItem logItem)
        {
            using (var dbContext = new LoggerDBContext(_connectionString.LoggerDBEntities))
            {
                dbContext.LogItems.Add(logItem);
                await dbContext.SaveChangesAsync();
            }
        }

        public async Task UpdateLog(LogItem logItem)
        {
            using (var dbContext = new LoggerDBContext(_connectionString.LoggerDBEntities))
            {
                var entity = dbContext.LogItems.FirstOrDefault(item => item.LogItemNumber == logItem.LogItemNumber);

                if (entity != null)
                {
                    // cambios 
                    entity.LogItemMessage = logItem.LogItemMessage;
                    entity.LogItemLevel = logItem.LogItemLevel;

                    /* If the entry is being tracked, then invoking update API is not needed. 
                      The API only needs to be invoked if the entry was not tracked. 
                      https://www.learnentityframeworkcore.com/dbcontext/modifying-data */
                    // context.Products.Update(entity);

                    await dbContext.SaveChangesAsync();
                }
            }
        }

        public async Task DeleteLog(LogItem logItem)
        {
            using (var dbContext = new LoggerDBContext(_connectionString.LoggerDBEntities))
            {
                var entity = dbContext.LogItems.FirstOrDefault(item => item.LogItemNumber == logItem.LogItemNumber);

                if (entity != null)
                {
                    dbContext.LogItems.Remove(entity);

                    await dbContext.SaveChangesAsync();
                }
            }
        }
    }
}
