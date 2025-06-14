using System.ComponentModel.DataAnnotations;

namespace SmartTask.Web.ViewModels
{
    public class OutlookEventViewModel
    {
        [Required]
        public string Subject { get; set; }

        public string Body { get; set; }

        [Required]
        public DateTime StartDateTime { get; set; }

        [Required]
        public DateTime EndDateTime { get; set; }

        public string Location { get; set; }
        public List<string> AttendeeEmails { get; set; }
    }
}
