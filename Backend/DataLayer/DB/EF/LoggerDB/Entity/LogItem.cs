using System;
using System.Collections.Generic;

namespace Academikus.AgenteInteligenteMentoresWebApi.Data.DB.EF.LoggerDB.Entity;

public partial class LogItem
{
    public long LogItemNumber { get; set; }

    public DateTime? LogItemDate { get; set; }

    public long? LogItemTimeStamp { get; set; }

    public string? LogItemSource { get; set; }

    public string? LogItemThread { get; set; }

    public string? LogItemLevel { get; set; }

    public string? LogItemLogger { get; set; }

    public string? LogItemMessage { get; set; }

    public string? LogItemException { get; set; }

    public long LogItemId { get; set; }
}
