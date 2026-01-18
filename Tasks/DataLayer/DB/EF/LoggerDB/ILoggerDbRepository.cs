using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.LoggerDB.Entity;

namespace Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.LoggerDB
{
    public interface ILoggerDbRepository
    {
        List<LogItem> GetLogs();
        Task InsertLog(LogItem logItem);
        Task UpdateLog(LogItem logItem);
        Task DeleteLog(LogItem logItem);
    }
}
