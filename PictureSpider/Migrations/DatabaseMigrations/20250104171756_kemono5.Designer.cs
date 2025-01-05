﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PictureSpider.Kemono;

#nullable disable

namespace PictureSpider.Migrations.DatabaseMigrations
{
    [DbContext(typeof(Database))]
    [Migration("20250104171756_kemono5")]
    partial class kemono5
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.5")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            MySqlModelBuilderExtensions.AutoIncrementColumns(modelBuilder);

            modelBuilder.Entity("PictureSpider.Kemono.ExternalWork", b =>
                {
                    b.Property<string>("id")
                        .HasColumnType("varchar(95)");

                    b.Property<int>("type")
                        .HasColumnType("int");

                    b.Property<bool>("excluded")
                        .HasColumnType("tinyint(1)");

                    b.Property<int>("index")
                        .HasColumnType("int");

                    b.Property<string>("name")
                        .HasColumnType("longtext");

                    b.Property<string>("url")
                        .HasColumnType("longtext");

                    b.Property<string>("workGroupid")
                        .HasColumnType("varchar(95)");

                    b.Property<string>("workGroupuserservice")
                        .HasColumnType("varchar(95)");

                    b.HasKey("id", "type");

                    b.HasIndex("workGroupid", "workGroupuserservice");

                    b.ToTable("ExternalWorks");
                });

            modelBuilder.Entity("PictureSpider.Kemono.User", b =>
                {
                    b.Property<string>("id")
                        .HasColumnType("varchar(95)");

                    b.Property<string>("service")
                        .HasColumnType("varchar(95)");

                    b.Property<string>("displayId")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("varchar(128)");

                    b.Property<string>("displayText")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("varchar(128)");

                    b.Property<bool>("dowloadEmbed")
                        .HasColumnType("tinyint(1)");

                    b.Property<bool>("dowloadExternalWorks")
                        .HasColumnType("tinyint(1)");

                    b.Property<bool>("dowloadWorks")
                        .HasColumnType("tinyint(1)");

                    b.Property<DateTime>("fetchedTime")
                        .HasColumnType("datetime");

                    b.Property<bool>("followed")
                        .HasColumnType("tinyint(1)");

                    b.Property<bool>("queued")
                        .HasColumnType("tinyint(1)");

                    b.HasKey("id", "service");

                    b.ToTable("Users");
                });

            modelBuilder.Entity("PictureSpider.Kemono.Work", b =>
                {
                    b.Property<string>("urlPath")
                        .HasColumnType("varchar(95)");

                    b.Property<string>("service")
                        .HasColumnType("varchar(95)");

                    b.Property<string>("coverGroupid")
                        .HasColumnType("varchar(95)");

                    b.Property<string>("coverGroupuserservice")
                        .HasColumnType("varchar(95)");

                    b.Property<bool>("excluded")
                        .HasColumnType("tinyint(1)");

                    b.Property<int>("index")
                        .HasColumnType("int");

                    b.Property<string>("name")
                        .HasColumnType("longtext");

                    b.Property<string>("urlHost")
                        .HasColumnType("longtext");

                    b.Property<string>("workGroupid")
                        .HasColumnType("varchar(95)");

                    b.Property<string>("workGroupuserservice")
                        .HasColumnType("varchar(95)");

                    b.HasKey("urlPath", "service");

                    b.HasIndex("coverGroupid", "coverGroupuserservice")
                        .IsUnique();

                    b.HasIndex("workGroupid", "workGroupuserservice");

                    b.ToTable("Works");
                });

            modelBuilder.Entity("PictureSpider.Kemono.WorkGroup", b =>
                {
                    b.Property<string>("id")
                        .HasColumnType("varchar(95)");

                    b.Property<string>("userservice")
                        .HasColumnType("varchar(95)");

                    b.Property<string>("desc")
                        .HasColumnType("longtext");

                    b.Property<string>("embedUrl")
                        .HasColumnType("longtext");

                    b.Property<bool>("fav")
                        .HasColumnType("tinyint(1)");

                    b.Property<bool>("fetched")
                        .HasColumnType("tinyint(1)");

                    b.Property<bool>("readed")
                        .HasColumnType("tinyint(1)");

                    b.Property<string>("title")
                        .HasColumnType("longtext");

                    b.Property<string>("userid")
                        .HasColumnType("varchar(95)");

                    b.HasKey("id", "userservice");

                    b.HasIndex("userid", "userservice");

                    b.ToTable("WorkGroups");
                });

            modelBuilder.Entity("PictureSpider.Kemono.ExternalWork", b =>
                {
                    b.HasOne("PictureSpider.Kemono.WorkGroup", "workGroup")
                        .WithMany("externalWorks")
                        .HasForeignKey("workGroupid", "workGroupuserservice");

                    b.Navigation("workGroup");
                });

            modelBuilder.Entity("PictureSpider.Kemono.Work", b =>
                {
                    b.HasOne("PictureSpider.Kemono.WorkGroup", "coverGroup")
                        .WithOne("cover")
                        .HasForeignKey("PictureSpider.Kemono.Work", "coverGroupid", "coverGroupuserservice");

                    b.HasOne("PictureSpider.Kemono.WorkGroup", "workGroup")
                        .WithMany("works")
                        .HasForeignKey("workGroupid", "workGroupuserservice")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.Navigation("coverGroup");

                    b.Navigation("workGroup");
                });

            modelBuilder.Entity("PictureSpider.Kemono.WorkGroup", b =>
                {
                    b.HasOne("PictureSpider.Kemono.User", "user")
                        .WithMany("workGroups")
                        .HasForeignKey("userid", "userservice");

                    b.Navigation("user");
                });

            modelBuilder.Entity("PictureSpider.Kemono.User", b =>
                {
                    b.Navigation("workGroups");
                });

            modelBuilder.Entity("PictureSpider.Kemono.WorkGroup", b =>
                {
                    b.Navigation("cover");

                    b.Navigation("externalWorks");

                    b.Navigation("works");
                });
#pragma warning restore 612, 618
        }
    }
}
