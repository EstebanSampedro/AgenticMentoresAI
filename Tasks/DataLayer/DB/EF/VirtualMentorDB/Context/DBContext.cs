using System;
using System.Collections.Generic;
using Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Entities;
using Microsoft.EntityFrameworkCore;

namespace Academikus.AgenteInteligenteMentoresTareas.Data.DB.EF.VirtualMentorDB.Context;

public partial class DBContext : DbContext
{
    public DBContext(DbContextOptions<DBContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AiPendingBatch> AiPendingBatches { get; set; }

    public virtual DbSet<Chat> Chats { get; set; }

    public virtual DbSet<ChatIalog> ChatIalogs { get; set; }

    public virtual DbSet<Conversation> Conversations { get; set; }

    public virtual DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

    public virtual DbSet<LroSession> LroSessions { get; set; }

    public virtual DbSet<Message> Messages { get; set; }

    public virtual DbSet<MessageAttachment> MessageAttachments { get; set; }

    public virtual DbSet<ProactiveMessage> ProactiveMessages { get; set; }

    public virtual DbSet<StudentContextInfo> StudentContextInfos { get; set; }

    public virtual DbSet<Summary> Summaries { get; set; }

    public virtual DbSet<TokenCache> TokenCaches { get; set; }

    public virtual DbSet<UserLeave> UserLeaves { get; set; }

    public virtual DbSet<UserTable> UserTables { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.UseCollation("Modern_Spanish_CI_AS");

        modelBuilder.Entity<AiPendingBatch>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__AiPendin__3214EC07161248DC");

            entity.ToTable("AiPendingBatch");

            entity.HasIndex(e => new { e.Status, e.WindowEndsAt }, "IX_AiPendingBatch_Status_WindowEndsAt");

            entity.HasIndex(e => e.ChatId, "UX_AiPendingBatch_Chat_Open")
                .IsUnique()
                .HasFilter("([Status] IN ('Pending', 'Processing'))");

            entity.Property(e => e.ChatId).HasMaxLength(100);
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
            entity.Property(e => e.LastMessageId).HasMaxLength(200);
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("Pending");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(100)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Chat>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Chat__3214EC07EF4A3EF3");

            entity.ToTable("Chat");

            entity.Property(e => e.ChatState)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Iaenabled).HasColumnName("IAEnabled");
            entity.Property(e => e.MsteamsChatId)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("MSTeamsChatId");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.Mentor).WithMany(p => p.ChatMentors)
                .HasForeignKey(d => d.MentorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Chats_Mentor");

            entity.HasOne(d => d.Student).WithMany(p => p.ChatStudents)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Chats_Student");
        });

        modelBuilder.Entity<ChatIalog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ChatIALo__3214EC0736427602");

            entity.ToTable("ChatIALog");

            entity.Property(e => e.CreatedBy)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.IachangeReason)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasDefaultValue("Configuración inicial por script")
                .HasColumnName("IAChangeReason");
            entity.Property(e => e.Iastate).HasColumnName("IAState");
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.Chat).WithMany(p => p.ChatIalogs)
                .HasForeignKey(d => d.ChatId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_ChatIALogs_Chats");
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Conversa__3214EC07D88374E7");

            entity.ToTable("Conversation");

            entity.Property(e => e.CreatedBy)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.Chat).WithMany(p => p.Conversations)
                .HasForeignKey(d => d.ChatId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Conversations_Chats");
        });

        modelBuilder.Entity<LroSession>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__LroSessi__3214EC07B892B986");

            entity.ToTable("LroSession");

            entity.Property(e => e.CreatedBy)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.LastUsedBy)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.UserObjectId).HasMaxLength(64);
            entity.Property(e => e.UserUpn).HasMaxLength(256);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Message__3214EC0792D63FC4");

            entity.ToTable("Message");

            entity.Property(e => e.CreatedBy)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.DeletedBy)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.MessageContent).IsUnicode(false);
            entity.Property(e => e.MessageContentType)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.MessageStatus)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.MsteamsMessageId)
                .HasMaxLength(50)
                .IsUnicode(false)
                .HasColumnName("MSTeamsMessageId");
            entity.Property(e => e.SenderRole)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.Conversation).WithMany(p => p.Messages)
                .HasForeignKey(d => d.ConversationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Messages_Conversations");
        });

        modelBuilder.Entity<MessageAttachment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__MessageA__3214EC0714B9B871");

            entity.ToTable("MessageAttachment");

            entity.Property(e => e.ContentType).HasMaxLength(100);
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.DeletedBy)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.DriveId).HasMaxLength(100);
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.InternalContentUrl).HasDefaultValue("");
            entity.Property(e => e.MimeType).HasMaxLength(100);
            entity.Property(e => e.SourceType).HasMaxLength(50);
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.Message).WithMany(p => p.MessageAttachments)
                .HasForeignKey(d => d.MessageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MessageAttachment_Messages");
        });

        modelBuilder.Entity<ProactiveMessage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Proactiv__3214EC0765C65DFB");

            entity.ToTable("ProactiveMessage");

            entity.HasIndex(e => e.MessageKey, "UQ__Proactiv__E03734E14E06AD44").IsUnique();

            entity.Property(e => e.CreatedBy)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.DeletedBy)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.MessageKey)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(100)
                .IsUnicode(false);
        });

        modelBuilder.Entity<StudentContextInfo>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__StudentC__3214EC072480C7C2");

            entity.ToTable("StudentContextInfo");

            entity.Property(e => e.Career).HasMaxLength(255);
            entity.Property(e => e.Faculty).HasMaxLength(255);
            entity.Property(e => e.FullName).HasMaxLength(255);
            entity.Property(e => e.Identification).HasMaxLength(20);
            entity.Property(e => e.Nickname).HasMaxLength(100);

            entity.HasOne(d => d.Student).WithMany(p => p.StudentContextInfos)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_StudentContextInfo_User");
        });

        modelBuilder.Entity<Summary>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Summary__3214EC074EB78B20");

            entity.ToTable("Summary");

            entity.Property(e => e.CreatedBy)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.EscalationReason).HasMaxLength(500);
            entity.Property(e => e.Priority).HasMaxLength(10);
            entity.Property(e => e.Summary1).HasColumnName("Summary");
            entity.Property(e => e.SummaryType)
                .HasMaxLength(30)
                .IsUnicode(false);
            entity.Property(e => e.Theme)
                .HasMaxLength(100)
                .IsUnicode(false);

            entity.HasOne(d => d.Chat).WithMany(p => p.Summaries)
                .HasForeignKey(d => d.ChatId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Summary_Chat");
        });

        modelBuilder.Entity<TokenCache>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__TokenCac__3214EC0701511AF3");

            entity.ToTable("TokenCache");

            entity.HasIndex(e => e.ExpiresAtTime, "Index_ExpiresAtTime");

            entity.Property(e => e.Id)
                .HasMaxLength(449)
                .UseCollation("SQL_Latin1_General_CP1_CS_AS");
        });

        modelBuilder.Entity<UserLeave>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__UserLeav__3214EC0757489908");

            entity.ToTable("UserLeave");

            entity.Property(e => e.CreatedBy)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.UserLeaveState)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.Mentor).WithMany(p => p.UserLeaves)
                .HasForeignKey(d => d.MentorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserLeaves_Mentor");
        });

        modelBuilder.Entity<UserTable>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__UserTabl__3214EC0768CB77EC");

            entity.ToTable("UserTable");

            entity.HasIndex(e => e.Email, "UQ__UserTabl__A9D10534C1010888").IsUnique();

            entity.Property(e => e.BackupEmail)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.BannerId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Career).HasMaxLength(50);
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.EntraUserId)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.Faculty).HasMaxLength(255);
            entity.Property(e => e.FavoriteName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.FullName)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Gender).HasMaxLength(1);
            entity.Property(e => e.Identification).HasMaxLength(20);
            entity.Property(e => e.Pidm)
                .HasMaxLength(20)
                .HasColumnName("PIDM");
            entity.Property(e => e.SpecialConsideration).IsUnicode(false);
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.UserRole)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.UserState)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.UserType)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
