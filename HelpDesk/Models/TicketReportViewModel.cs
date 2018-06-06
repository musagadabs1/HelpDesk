using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HelpDesk.Models
{
    public class TicketReportViewModel
    {
        public string TicketNo { get; set; }
        public string Subject { get; set; }
        public string Description { get; set; }
        public DateTime CreationDate { get; set; }
        public string CreatedBy { get; set; }
        public string Status { get; set; }
    }
}