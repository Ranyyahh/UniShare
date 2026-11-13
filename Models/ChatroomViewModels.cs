using System;
using System.Collections.Generic;

namespace UniShare.Models
{
    public class ChatConversationViewModel
    {
        public string Id { get; set; }
        public string With { get; set; }
        public string Initials { get; set; }
        public string ItemName { get; set; }
        public string LastMessage { get; set; }
        public string Time { get; set; }
        public bool Unread { get; set; }
        public string ItemId { get; set; }
        public string ItemImage { get; set; }
        public string ItemUrl { get; set; }
        public string ItemPrice { get; set; }
        public string RentalUrl { get; set; }
    }

    public class ChatMessageViewModel
    {
        public string Id { get; set; }
        public bool Mine { get; set; }
        public string Text { get; set; }
        public string Time { get; set; }
        public string Day { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ChatThreadViewModel
    {
        public string Id { get; set; }
        public string With { get; set; }
        public string Initials { get; set; }
        public string ItemName { get; set; }
        public string ItemId { get; set; }
        public string ItemImage { get; set; }
        public string ItemUrl { get; set; }
        public string ItemPrice { get; set; }
        public string RentalUrl { get; set; }
        public List<ChatMessageViewModel> Messages { get; set; } = new List<ChatMessageViewModel>();
    }
}
