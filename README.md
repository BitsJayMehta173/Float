☁️ FloatingReminder

FloatingReminder is a collaborative, real-time reminder app for Windows. It's designed to display persistent, on-screen reminders from a shared, cloud-synced collection.

It functions as a "Google Doc" for your to-do list, where you can invite friends to a reminder collection and see their changes (and your own) sync live across all connected devices.


✨ Core Features

Real-Time Collaboration: Invite friends to your reminder collections. All changes sync live across all users and devices, powered by MongoDB Change Streams.

Persistent Floating Window: An always-on-top, borderless window displays your reminders, cycling through them one by one.

Social System: A complete friends list, user search, and friend request system.

Full Ownership Control: Collections have a single owner. Non-owners can't delete or re-share, but they can create a private "Copy" of any collection for themselves.

Detailed Reminders: Add, edit, and delete reminders. Each item tracks its original author and creation date.

Secure Authentication: Full user login and registration system with passwords securely hashed using BCrypt.

Offline-First Caching: All collections are cached locally as JSON. The app is fully functional offline and syncs all changes when you reconnect.

Modern UI: A sleek, glass-morphism inspired WPF interface that runs in the system tray.

💻 Tech Stack

Framework: WPF on .NET 4.7.2

Database: MongoDB Atlas

Real-Time Sync: MongoDB Change Streams

Libraries:

MongoDB.Driver

BCrypt.Net-Next (for password hashing)

Newtonsoft.Json (for local caching)

🚀 How to Run

To clone and run this project, you will need to set up your own MongoDB Atlas database.

Clone the Repository:

git clone [https://github.com/your-username/FloatingReminder.git](https://github.com/BitsJayMehta173/Float))


Restore NuGet Packages:
Open the solution (FloatingReminder.sln) in Visual Studio and build it. Visual Studio should automatically restore all the required packages from packages.config.

Set Up MongoDB:

This project is built to connect to a MongoDB Atlas cluster.

Create a new, free M0 cluster on MongoDB Atlas.

In your new cluster, create a database named FloatingReminderDB.

This database must have three collections (MongoDB will create them on first use, but you can create them manually):

Users

FriendRequests

AllCollections

Run the App:
Set FloatingReminder as the startup project and press F5 to run. You can now create an account and log in.
