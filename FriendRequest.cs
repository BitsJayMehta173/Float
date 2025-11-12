using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace FloatingReminder
{
    // This enum will track the state of a request
    public enum RequestStatus
    {
        Pending,
        Accepted,
        Declined
    }

    public class FriendRequest
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement("senderUsername")]
        public string SenderUsername { get; set; }

        [BsonElement("recipientUsername")]
        public string RecipientUsername { get; set; }

        [BsonElement("status")]
        public RequestStatus Status { get; set; }

        [BsonElement("sentAt")]
        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime SentAt { get; set; }

        public FriendRequest()
        {
            this.SentAt = DateTime.UtcNow;
            this.Status = RequestStatus.Pending;
        }
    }
}