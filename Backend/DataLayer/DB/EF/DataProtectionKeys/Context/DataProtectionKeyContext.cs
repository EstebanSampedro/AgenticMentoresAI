using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Academikus.AgenteInteligenteMentoresWebApi.Data.DB.EF.DataProtectionKeys.Context;

public sealed class DataProtectionKeyContext : DbContext, IDataProtectionKeyContext
{
    public DataProtectionKeyContext(DbContextOptions<DataProtectionKeyContext> options) : base(options) { }
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<DataProtectionKey>().ToTable("DataProtectionKeys", "dbo");
        base.OnModelCreating(b);
    }
}
