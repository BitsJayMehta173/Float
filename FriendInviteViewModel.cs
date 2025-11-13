namespace FloatingReminder
{
    // Helper class for the invite window's checklist
    public class FriendInviteViewModel
    {
        public string Username { get; set; }

        // This is the checkbox
        public bool IsInvited { get; set; }

        // This is to disable the checkbox if they are already invited
        public bool CanInvite { get; set; }
    }
}