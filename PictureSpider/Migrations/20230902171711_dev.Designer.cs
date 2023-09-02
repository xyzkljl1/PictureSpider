﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PictureSpider.Hitomi;

#nullable disable

namespace PictureSpider.Migrations
{
    [DbContext(typeof(Database))]
    [Migration("20230902171711_dev")]
    partial class dev
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.10")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("PictureSpider.Hitomi.Illust", b =>
                {
                    b.Property<string>("hash")
                        .HasColumnType("varchar(95)");

                    b.Property<int?>("illustGroupid")
                        .HasColumnType("int");

                    b.Property<string>("url")
                        .HasColumnType("longtext");

                    b.HasKey("hash");

                    b.HasIndex("illustGroupid");

                    b.ToTable("Illusts");
                });

            modelBuilder.Entity("PictureSpider.Hitomi.IllustGroup", b =>
                {
                    b.Property<int>("id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    b.Property<string>("PageURL")
                        .HasColumnType("longtext");

                    b.Property<string>("userid")
                        .HasColumnType("varchar(95)");

                    b.HasKey("id");

                    b.HasIndex("userid");

                    b.ToTable("IllustGroups");
                });

            modelBuilder.Entity("PictureSpider.Hitomi.User", b =>
                {
                    b.Property<string>("id")
                        .HasColumnType("varchar(95)");

                    b.Property<string>("displayId")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("varchar(128)");

                    b.Property<string>("displayText")
                        .IsRequired()
                        .HasMaxLength(128)
                        .HasColumnType("varchar(128)");

                    b.Property<bool>("followed")
                        .HasColumnType("tinyint(1)");

                    b.Property<bool>("queued")
                        .HasColumnType("tinyint(1)");

                    b.HasKey("id");

                    b.ToTable("Users");
                });

            modelBuilder.Entity("PictureSpider.Hitomi.Illust", b =>
                {
                    b.HasOne("PictureSpider.Hitomi.IllustGroup", "illustGroup")
                        .WithMany("illusts")
                        .HasForeignKey("illustGroupid");

                    b.Navigation("illustGroup");
                });

            modelBuilder.Entity("PictureSpider.Hitomi.IllustGroup", b =>
                {
                    b.HasOne("PictureSpider.Hitomi.User", "user")
                        .WithMany("illustGroups")
                        .HasForeignKey("userid");

                    b.Navigation("user");
                });

            modelBuilder.Entity("PictureSpider.Hitomi.IllustGroup", b =>
                {
                    b.Navigation("illusts");
                });

            modelBuilder.Entity("PictureSpider.Hitomi.User", b =>
                {
                    b.Navigation("illustGroups");
                });
#pragma warning restore 612, 618
        }
    }
}
