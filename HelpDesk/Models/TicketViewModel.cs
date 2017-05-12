using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;

namespace HelpDesk.Models
{
    public class TicketViewModel
    {
        public int SN { get; set; }
        public Guid TicketID { get; set; }
        public string TicketNo { get; set; }
        public string OwnerEmail { get; set; }
        [Required]
        public Guid CategoryID { get; set; }
        public DAL.Category Category { get; set; }
        [Required]
        public string Subject { get; set; }
        [Required]
        public string Description { get; set; }
        [Required]
        public string Status { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreationDate { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime ModifiedDate { get; set; }
        public bool IsDeleted { get; set; }
        public HttpPostedFileBase[] FileAttachments { get; set; }
        public List<TicketFileViewModel> TicketFileViewModels { get; set; }
        public List<TicketNoteViewModel> TicketNoteViewModels { get; set; }

        public TicketViewModel()
        {
            this.TicketFileViewModels = new List<TicketFileViewModel>();
            this.TicketNoteViewModels = new List<TicketNoteViewModel>();
        }
    }

    public class CategoryViewModel
    {
        public Guid CategoryID { get; set; }
        public string Category { get; set; }
        public List<TicketViewModel> TicketViewModels { get; set; }
    }

    public class TicketFileViewModel
    {
        public Guid TicketFileID { get; set; }
        public Guid TicketID { get; set; }
        public string FileName { get; set; }
        public string FileForTOrN { get; set; }
        public Guid RefID { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreationDate { get; set; }
        public TicketViewModel TicketViewModel { get; set; }
    }

    public class TicketNoteViewModel
    {
        public Guid TicketNoteID { get; set; }
        public Guid TicketID { get; set; }
        public string Note { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreationDate { get; set; }
        public TicketViewModel TicketViewModel { get; set; }
    }
}