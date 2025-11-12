using Microsoft.EntityFrameworkCore;
using System;
using System.IO;

// Make sure this namespace matches your project's namespace
namespace FloatingNote
{
    public class ReminderContext : DbContext
    {
        // This tells Entity Framework we want a table called "ReminderItems"
        // that will store our ReminderItem objects
        public DbSet<ReminderItem> ReminderItems { get; set; }

        // This configures the connection to our local SQLite file
        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            // Get the path to the user's local app data folder
            // This is the standard, safe place to store app data
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // We'll create a special folder for our app inside AppData
            string dbFolder = Path.Combine(appDataPath, "FloatingReminder");

            // Ensure this folder exists
            Directory.CreateDirectory(dbFolder);

            // This is the full path to our database file
            string dbPath = Path.Combine(dbFolder, "reminders.db");

            // Tell EF Core to use this SQLite file
            options.UseSqlite($"Data Source={dbPath}");
        }
    }
}