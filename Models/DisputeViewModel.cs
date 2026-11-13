using System;

namespace UniShare.Models
{
    public class DisputeViewModel
    {
        public string DisputeId { get; set; }
        public string BookingId { get; set; }
        public string ItemName { get; set; }
        public string ReporterName { get; set; }
        public string Reporter
        {
            get => ReporterName;
            set => ReporterName = value;
        }
        public string ReporterId { get; set; }
        public string RespondentName { get; set; }
        public string Respondent
        {
            get => RespondentName;
            set => RespondentName = value;
        }
        public string RespondentId { get; set; }
        public string SerialNumber { get; set; }
        public string Priority { get; set; }
        public string Status { get; set; }
        public string DateReported { get; set; }
        public string IncidentStatement { get; set; }
    }
}
