﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PictureSpider.LocalSingleFile;

#nullable disable

namespace PictureSpider.Migrations.DatabaseMigrations
{
    [DbContext(typeof(Database))]
    [Migration("20231220111101_lsf_dev2")]
    partial class lsf_dev2
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.10")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            modelBuilder.Entity("PictureSpider.LocalSingleFile.Illust", b =>
                {
                    b.Property<string>("path")
                        .HasColumnType("varchar(95)");

                    b.Property<DateTime>("date")
                        .HasColumnType("datetime");

                    b.HasKey("path");

                    b.ToTable("Readed");
                });
#pragma warning restore 612, 618
        }
    }
}
