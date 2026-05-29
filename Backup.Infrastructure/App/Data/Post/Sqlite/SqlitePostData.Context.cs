using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Backup.App.Data.Posts;

public partial class SqlitePostData
{
    private sealed class PostsDbContext(string dbPath) : DbContext
    {
        private readonly string _dbPath = dbPath;

        public DbSet<PostProfileEntity> Profiles => Set<PostProfileEntity>();
        public DbSet<PostEntity> Posts => Set<PostEntity>();
        public DbSet<PostHashtagEntity> PostHashtags => Set<PostHashtagEntity>();
        public DbSet<PostMediaEntity> PostMedias => Set<PostMediaEntity>();
        public DbSet<PostMediaVariantEntity> PostMediaVariants => Set<PostMediaVariantEntity>();
        public DbSet<PostIndexEntryEntity> PostIndexEntries => Set<PostIndexEntryEntity>();
        public DbSet<PostChangeEntity> PostChanges => Set<PostChangeEntity>();
        public DbSet<PostChangeFieldEntity> PostChangeFields => Set<PostChangeFieldEntity>();
        public DbSet<PostHashMetaEntity> PostHashMeta => Set<PostHashMetaEntity>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            SqliteConnectionStringBuilder connection = new()
            {
                DataSource = _dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
                DefaultTimeout = 30,
            };

            optionsBuilder.UseSqlite(connection.ToString());
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PostProfileEntity>(entity =>
            {
                entity.ToTable("profiles");
                entity.HasKey(o => o.Id);
                entity.Property(o => o.Id).IsRequired();
            });

            modelBuilder.Entity<PostEntity>(entity =>
            {
                entity.ToTable("posts");
                entity.HasKey(o => o.Id);
                entity.Property(o => o.Id).IsRequired();
                entity.Property(o => o.ProfileId).IsRequired();
                entity.Property(o => o.Description).IsRequired();
                entity.Property(o => o.CreatedAt).IsRequired();

                entity
                    .HasOne(o => o.Profile)
                    .WithMany(o => o.Posts)
                    .HasForeignKey(o => o.ProfileId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity
                    .HasMany(o => o.Hashtags)
                    .WithOne(o => o.Post)
                    .HasForeignKey(o => o.PostId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity
                    .HasMany(o => o.Medias)
                    .WithOne(o => o.Post)
                    .HasForeignKey(o => o.PostId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity
                    .HasMany(o => o.Changes)
                    .WithOne(o => o.Post)
                    .HasForeignKey(o => o.PostId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity
                    .HasMany(o => o.IndexEntries)
                    .WithOne(o => o.Post)
                    .HasForeignKey(o => o.PostId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PostHashtagEntity>(entity =>
            {
                entity.ToTable("post_hashtags");
                entity.HasKey(o => o.Id);
                entity.Property(o => o.PostId).IsRequired();
                entity.Property(o => o.Value).IsRequired();
                entity.HasIndex(o => new { o.PostId, o.Ordinal });
            });

            modelBuilder.Entity<PostMediaEntity>(entity =>
            {
                entity.ToTable("post_medias");
                entity.HasKey(o => o.Id);
                entity.Property(o => o.PostId).IsRequired();
                entity.Property(o => o.MediaId).IsRequired();
                entity.Property(o => o.Url).IsRequired();
                entity.Property(o => o.Type).IsRequired();
                entity.HasIndex(o => new { o.PostId, o.Ordinal });

                entity
                    .HasMany(o => o.Variants)
                    .WithOne(o => o.Media)
                    .HasForeignKey(o => o.MediaRefId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PostMediaVariantEntity>(entity =>
            {
                entity.ToTable("post_media_variants");
                entity.HasKey(o => o.Id);
                entity.Property(o => o.ContentType).IsRequired();
                entity.Property(o => o.Url).IsRequired();
                entity.HasIndex(o => new { o.MediaRefId, o.Ordinal });
            });

            modelBuilder.Entity<PostIndexEntryEntity>(entity =>
            {
                entity.ToTable("post_index_entries");
                entity.HasKey(o => o.Id);
                entity.Property(o => o.PostId).IsRequired();
                entity.Property(o => o.UserId).IsRequired();
                entity.Property(o => o.Origin).IsRequired();
                entity
                    .HasIndex(o => new
                    {
                        o.PostId,
                        o.UserId,
                        o.Origin,
                    })
                    .IsUnique();
                entity.HasIndex(o => new
                {
                    o.UserId,
                    o.Origin,
                    o.PostId,
                });
            });

            modelBuilder.Entity<PostChangeEntity>(entity =>
            {
                entity.ToTable("post_changes");
                entity.HasKey(o => o.Id);
                entity.Property(o => o.PostId).IsRequired();
                entity.Property(o => o.UserId).IsRequired();
                entity.Property(o => o.ChangeType).IsRequired();
                entity.HasIndex(o => new
                {
                    o.PostId,
                    o.Date,
                    o.Id,
                });

                entity
                    .HasMany(o => o.Fields)
                    .WithOne(o => o.Change)
                    .HasForeignKey(o => o.ChangeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PostChangeFieldEntity>(entity =>
            {
                entity.ToTable("post_change_fields");
                entity.HasKey(o => o.Id);
                entity.Property(o => o.Field).IsRequired();
                entity.HasIndex(o => new { o.ChangeId, o.Field });
            });

            modelBuilder.Entity<PostHashMetaEntity>(entity =>
            {
                entity.ToTable("post_meta");
                entity.HasKey(o => o.Id);
                entity.Property(o => o.Id).IsRequired();
                entity.Property(o => o.Hash).IsRequired();
            });

            ApplySnakeCaseColumnNames(modelBuilder);
        }

        private static void ApplySnakeCaseColumnNames(ModelBuilder modelBuilder)
        {
            foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (IMutableProperty property in entityType.GetProperties())
                    property.SetColumnName(ToSnakeCase(property.Name));
            }
        }

        private static string ToSnakeCase(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            var chars = new List<char>(value.Length + 8);

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool isUpper = char.IsUpper(c);

                if (isUpper && i > 0)
                {
                    char prev = value[i - 1];
                    bool prevIsLowerOrDigit = char.IsLower(prev) || char.IsDigit(prev);
                    bool prevIsUpperThenNextLower =
                        char.IsUpper(prev) && i + 1 < value.Length && char.IsLower(value[i + 1]);

                    if (prevIsLowerOrDigit || prevIsUpperThenNextLower)
                        chars.Add('_');
                }

                chars.Add(char.ToLowerInvariant(c));
            }

            return new string(chars.ToArray());
        }
    }
}
