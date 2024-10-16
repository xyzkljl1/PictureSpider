﻿// <auto-generated />
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PictureSpider.Telegram;

#nullable disable

namespace PictureSpider.Migrations.DatabaseMigrations
{
    [DbContext(typeof(Database))]
    partial class DatabaseModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.5")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            MySqlModelBuilderExtensions.AutoIncrementColumns(modelBuilder);

            modelBuilder.Entity("PictureSpider.Telegram.Channel", b =>
                {
                    b.Property<long>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<long>("id"));

                    b.Property<bool>("download_comments")
                        .HasColumnType("tinyint(1)");

                    b.Property<bool>("download_illust")
                        .HasColumnType("tinyint(1)");

                    b.Property<bool>("download_telegraph")
                        .HasColumnType("tinyint(1)");

                    b.Property<bool>("download_video")
                        .HasColumnType("tinyint(1)");

                    b.Property<int>("end_timestamp")
                        .HasColumnType("int");

                    b.Property<int>("start_timestamp")
                        .HasColumnType("int");

                    b.Property<string>("title")
                        .HasMaxLength(600)
                        .HasColumnType("varchar(600)");

                    b.Property<string>("username")
                        .HasMaxLength(600)
                        .HasColumnType("varchar(600)");

                    b.HasKey("id");

                    b.ToTable("Channels");
                });

            modelBuilder.Entity("PictureSpider.Telegram.FinishedTask", b =>
                {
                    b.Property<long>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<long>("id"));

                    b.Property<string>("comment")
                        .HasColumnType("longtext");

                    b.Property<string>("fileid")
                        .HasMaxLength(100)
                        .HasColumnType("varchar(100)");

                    b.Property<string>("title")
                        .HasMaxLength(400)
                        .HasColumnType("varchar(400)");

                    b.Property<string>("url")
                        .HasMaxLength(600)
                        .HasColumnType("varchar(600)");

                    b.HasKey("id");

                    b.HasIndex("fileid");

                    b.HasIndex("title");

                    b.HasIndex("url");

                    b.ToTable("FinishedTasks");
                });

            modelBuilder.Entity("PictureSpider.Telegram.Message", b =>
                {
                    b.Property<long>("id")
                        .HasColumnType("bigint");

                    b.Property<long>("chat")
                        .HasColumnType("bigint");

                    b.Property<long>("albumid")
                        .HasColumnType("bigint");

                    b.Property<int>("downloadType")
                        .HasColumnType("int");

                    b.Property<string>("json")
                        .HasColumnType("longtext");

                    b.Property<string>("localPath")
                        .HasMaxLength(600)
                        .HasColumnType("varchar(600)");

                    b.Property<int>("state")
                        .HasColumnType("int");

                    b.Property<int>("timestamp")
                        .HasColumnType("int");

                    b.HasKey("id", "chat");

                    b.HasIndex("albumid");

                    b.ToTable("Messages");
                });
#pragma warning restore 612, 618
        }
    }
}
