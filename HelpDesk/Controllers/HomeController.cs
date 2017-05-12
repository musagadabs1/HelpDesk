using HelpDesk.DAL;
using HelpDesk.Models;
using IEIA.CommonUtil.Alert.Email;
using IEIA.CommonUtil.Web;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace HelpDesk.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private string fileAttachmentPath = "~/Content/Uploads/FileAttachments";

        public static string CalculateAge(DateTime date1, DateTime date2)
        {
            string result = string.Empty;

            if (date2 == null)
                date2 = DateTime.Now;

            TimeSpan difference = date2.Subtract(date1);

            int years = Convert.ToInt32((difference.TotalDays / 30) % 12),
                months = Convert.ToInt32(difference.TotalDays / 30),
                days = Convert.ToInt32(difference.TotalDays),
                hours = Convert.ToInt32(difference.TotalHours),
                minutes = Convert.ToInt32(difference.TotalMinutes),
                seconds = Convert.ToInt32(difference.TotalSeconds);

            if (years > 0)
                return string.Format("{0} year{1} ago", years, years > 1 ? "s" : "");
            if (months > 0)
                return string.Format("{0} month{1} ago", months, months > 1 ? "s" : "");
            if (days > 0)
                return string.Format("{0} day{1} ago", days, days > 1 ? "s" : "");
            if (hours > 0)
                return string.Format("{0} hour{1} ago", hours, hours > 1 ? "s" : "");
            if (minutes > 0)
                return string.Format("{0} minute{1} ago", minutes, minutes > 1 ? "s" : "");
            if (seconds > 0)
                return string.Format("{0} second{1} ago", seconds, seconds > 1 ? "s" : "");

            return "";
        }

        public static string GetTicketStatus(string status)
        {
            string result = "Unknown";

            switch (status)
            {
                case "N":
                    result = "New/Pending";
                    break;
                case "I":
                    result = "In progress";
                    break;
                case "T":
                    result = "Technician (ICT) responded";
                    break;
                case "D":
                    result = "Note added";
                    break;
                case "A":
                    result = "Author responded";
                    break;
                case "R":
                    result = "Resolved";
                    break;
                case "C":
                    result = "Closed successfully";
                    break;
                case "U":
                    result = "Closed unsuccessfully";
                    break;
                case "O":
                    result = "Reopened";
                    break;
            }

            return result;
        }

        public static bool IsMember(string userName, string groupName)
        {
            try
            {
                var pc = new PrincipalContext(ContextType.Domain, ConfigurationManager.AppSettings["ADIP"].ToString(), ConfigurationManager.AppSettings["ADAU"].ToString(), ConfigurationManager.AppSettings["ADAP"].ToString());
                var group = GroupPrincipal.FindByIdentity(pc, groupName);
                var members = group.GetMembers(true);
                var isInGroup = group.GetMembers(true).Where(p => p.UserPrincipalName.ToLowerInvariant() == userName.ToLowerInvariant() + "@ieianchorpensions.net").Any();
                return isInGroup;
            }
            catch (Exception E)
            {
                throw E;
            }
        }

        public async Task<ActionResult> Index()
        {
            List<TicketViewModel> tickets = await this.GetTickets(null, User.Identity.Name);
            return View(tickets);
        }

        private async Task<List<TicketViewModel>> GetTickets(string status, string userName)
        {
            List<TicketViewModel> tickets = new List<TicketViewModel>();
            using (HelpDeskEntities dataContext = new HelpDeskEntities())
            {
                var data = new List<Ticket>();

                if (!string.IsNullOrWhiteSpace(status) && !string.IsNullOrWhiteSpace(userName))
                {
                    data = await dataContext.Tickets.Include("Category").Where(t => t.IsDeleted.Equals(false) && t.Status.Equals(status) && t.CreatedBy.Equals(userName)).OrderByDescending(t => t.ModifiedDate).ToListAsync();
                }
                else if (!string.IsNullOrWhiteSpace(status) && string.IsNullOrWhiteSpace(userName))
                {
                    data = await dataContext.Tickets.Include("Category").Where(t => t.IsDeleted.Equals(false) && t.Status.Equals(status)).OrderByDescending(t => t.ModifiedDate).ToListAsync();
                }
                else if (string.IsNullOrWhiteSpace(status) && !string.IsNullOrWhiteSpace(userName))
                {
                    data = await dataContext.Tickets.Include("Category").Where(t => t.IsDeleted.Equals(false) && t.CreatedBy.Equals(userName)).OrderByDescending(t => t.ModifiedDate).ToListAsync();
                }
                else
                {
                    data = await dataContext.Tickets.Include("Category").Where(t => t.IsDeleted.Equals(false)).OrderByDescending(t => t.ModifiedDate).ToListAsync();
                }

                if (data.Count > 0)
                {
                    int i = 1;
                    foreach (Ticket ticket in data)
                    {
                        tickets.Add(new TicketViewModel()
                        {
                            SN = i,
                            TicketID = ticket.TicketID,
                            TicketNo = ticket.TicketNo,
                            Subject = ticket.Subject,
                            Description = ticket.Description,
                            Status = ticket.Status,
                            CreatedBy = ticket.CreatedBy,
                            CreationDate = ticket.CreationDate,
                            CategoryID = ticket.CategoryID,
                            ModifiedBy = ticket.ModifiedBy,
                            ModifiedDate = ticket.ModifiedDate,
                            Category = ticket.Category
                        });
                        i++;
                    }
                }
            }

            return tickets;
        }

        public async Task<JsonResult> GetTickets(string status)
        {
            var tickets = await this.GetTickets(status, string.Empty);

            return Json(tickets, JsonRequestBehavior.AllowGet);
        }

        public async Task<ActionResult> Tickets(string status = "N")
        {
            List<TicketViewModel> tickets = await this.GetTickets(status, null);
            return View(tickets);
        }

        [HttpGet]
        public async Task<ActionResult> RaiseTicket()
        {
            using (HelpDeskEntities dataContext = new HelpDeskEntities())
            {
                ViewBag.Categories = await dataContext.Categories.ToListAsync();
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RaiseTicket(TicketViewModel model)
        {
            Guid id = Guid.NewGuid();
            bool isSuccessful = false;
            bool sendEmailAlert = false;
            GeneralSetting generalSetting = null;

            try
            {
                ViewBag.Categories = await new HelpDeskEntities().Categories.ToListAsync();

                #region validate inputs
                StringBuilder errors = new StringBuilder();

                if (string.IsNullOrWhiteSpace(model.Subject))
                    errors.AppendLine("Subject is required.");
                if (string.IsNullOrWhiteSpace(model.Description))
                    errors.AppendLine("Description is required.");
                if (model.CategoryID == null)
                    errors.AppendLine("Category is required.");
                if (model.FileAttachments != null)
                {
                    string[] unsupportedFileTypes = new[] { "exe" };
                }
                #endregion

                if (errors.Length > 0)
                {
                    for (int i = 0; i < errors.Length; i++)
                    {
                        ModelState.AddModelError("Error" + i.ToString(), errors[i].ToString());
                    }
                }
                else
                {
                    // sanitize data
                    model.Subject = SecurityHelper.Sanitize(model.Subject);
                    model.Description = SecurityHelper.Sanitize(model.Description);

                    #region save data
                    using (TransactionScope transactionScope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                    {
                        using (HelpDeskEntities dataContext = new HelpDeskEntities())
                        {
                            string ticketNo = StringHelper.GenerateRandomNumber(5);
                            model.TicketNo = ticketNo;
                            Ticket ticket = new Ticket()
                            {
                                TicketID = id,
                                TicketNo = ticketNo,
                                CategoryID = model.CategoryID,
                                Subject = model.Subject,
                                Description = model.Description,
                                Status = "N",
                                CreatedBy = User.Identity.Name,
                                CreationDate = DateTime.Now,
                                ModifiedBy = User.Identity.Name,
                                ModifiedDate = DateTime.Now,
                                OwnerEmail = Session["userEmail"].ToString(),
                                IsDeleted = false
                            };

                            dataContext.Tickets.Add(ticket);

                            if (await dataContext.SaveChangesAsync() > 0)
                            {
                                #region ticket files
                                if (model.FileAttachments != null)
                                {
                                    if (model.FileAttachments.Length > 0)
                                    {
                                        string targetPath = HttpContext.Server.MapPath(this.fileAttachmentPath);

                                        if (model.FileAttachments[0] != null)
                                        {
                                            List<TicketFile> ticketFiles = new List<TicketFile>();

                                            foreach (var file in model.FileAttachments)
                                            {
                                                string fileName = Path.Combine(targetPath, file.FileName);
                                                file.SaveAs(fileName);

                                                ticketFiles.Add(new TicketFile()
                                                {
                                                    TicketFileID = Guid.NewGuid(),
                                                    TicketID = ticket.TicketID,
                                                    FileName = file.FileName,
                                                    FileForTOrN = "T",
                                                    RefID = id,
                                                    CreatedBy = User.Identity.Name,
                                                    CreationDate = DateTime.Now,
                                                    ModifiedBy = User.Identity.Name,
                                                    ModifiedDate = DateTime.Now,
                                                    IsDeleted = false
                                                });
                                            }

                                            dataContext.TicketFiles.AddRange(ticketFiles);

                                            await dataContext.SaveChangesAsync();
                                        }
                                    }
                                }
                                #endregion

                                // trigger e-mail [optional]
                                generalSetting = await dataContext.GeneralSettings.FirstOrDefaultAsync();
                                if (generalSetting != null)
                                {
                                    if (generalSetting.EnableEmailAlert && !string.IsNullOrWhiteSpace(generalSetting.RecipientEmails))
                                    {
                                        sendEmailAlert = true;
                                    }
                                }

                                transactionScope.Complete();
                                isSuccessful = true;
                            }
                            else
                            {
                                errors.AppendLine("Unknow error occured.");
                            }
                        }
                    }
                    #endregion
                }
            }
            catch (DbEntityValidationException ex)
            {
                HomeController.LogError(ex, HttpContext.Server.MapPath("~/Error_Log.txt"));
            }
            catch (Exception ex)
            {
                HomeController.LogError(ex, HttpContext.Server.MapPath("~/Error_Log.txt"));
            }

            if (isSuccessful)
            {
                if (sendEmailAlert)
                    SendEmail("ICT HelpDesk [" + model.TicketNo + "] " + model.Subject, model.Description, generalSetting.RecipientEmails, id.ToString(), model.FileAttachments);

                return RedirectToAction("TicketDetails", new RouteValueDictionary(new { controller = "Home", action = "TicketDetails", id = id }));
            }

            return View(model);
        }

        public void SendEmail(string subject, string mailBody, string recipients, string ticketID, HttpPostedFileBase[] fileAttachments = null)
        {
            try
            {
                SmtpClient smtpClient = new SmtpClient(ConfigurationManager.AppSettings["SmtpHost"], int.Parse(ConfigurationManager.AppSettings["SmtpPort"]));

                smtpClient.Credentials = new NetworkCredential(ConfigurationManager.AppSettings["EmailSenderUserName"], ConfigurationManager.AppSettings["EmailSenderPassword"]);
                smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
                smtpClient.EnableSsl = bool.Parse(ConfigurationManager.AppSettings["SmtpSSL"]);
                MailMessage mail = new MailMessage();

                mail.From = new MailAddress(ConfigurationManager.AppSettings["EmailSenderEmail"], ConfigurationManager.AppSettings["EmailSenderName"]);

                foreach (string email in recipients.Split(new char[] { ',' }))
                {
                    mail.To.Add(new MailAddress(email));
                }

                mailBody = mailBody +
                            "<p>Click <a href=\"" + SecurityHelper.BuildAbsolute("Home/TicketDetails/" + ticketID) + "\" title=\"View Ticket\">here</a></p>" +
                            "<p><strong>" + User.Identity.Name + "</strong></p>" +
                            "<p><strong>ICT HelpDesk</strong></p>" +
                            "<img src=\"" + SecurityHelper.BuildAbsolute("Content/assets/img/logo.png") + "\" alt=\"IEIA-Logo\" />";

                if (fileAttachments != null)
                {
                    if (fileAttachments.Length > 0)
                    {
                        string targetPath = HttpContext.Server.MapPath(this.fileAttachmentPath);

                        if (fileAttachments[0] != null)
                        {
                            List<TicketFile> ticketFiles = new List<TicketFile>();

                            foreach (var file in fileAttachments)
                            {
                                string fileName = Path.Combine(targetPath, file.FileName);
                                mail.Attachments.Add(new Attachment(fileName));
                            }
                        }
                    }
                }

                mail.Subject = subject;
                mail.Body = mailBody;
                mail.IsBodyHtml = true;

                smtpClient.Send(mail);
            }
            catch (Exception ex)
            {
                HomeController.LogError(ex, HttpContext.Server.MapPath("~/Error_Log.txt"));
            }
        }

        [HttpGet]
        public async Task<ActionResult> TicketDetails(Guid id, string errors = "", string subject = "", string mailBody = "", string recipients = "")
        {
            Ticket ticket = new Ticket();
            TicketViewModel ticketViewModel = new TicketViewModel();
            try
            {
                using (HelpDeskEntities dataContext = new HelpDeskEntities())
                {
                    ticket = await dataContext.Tickets.Include("Category").FirstOrDefaultAsync(p => p.TicketID.Equals(id));

                    if (ticket != null)
                    {
                        ticket.TicketFiles = await dataContext.TicketFiles.Where(p => p.TicketID.Equals(id)).ToListAsync();
                        ticket.TicketNotes = await dataContext.TicketNotes.Where(p => p.TicketID.Equals(id)).ToListAsync();

                        ticketViewModel.TicketID = id;
                        ticketViewModel.TicketNo = ticket.TicketNo;
                        ticketViewModel.CategoryID = ticket.CategoryID;
                        ticketViewModel.Category = ticket.Category;
                        ticketViewModel.Description = ticket.Description;
                        ticketViewModel.Status = ticket.Status;
                        ticketViewModel.CreatedBy = ticket.CreatedBy;
                        ticketViewModel.CreationDate = ticket.CreationDate;
                        ticketViewModel.ModifiedBy = ticket.ModifiedBy;
                        ticketViewModel.ModifiedDate = ticket.ModifiedDate;
                        ticketViewModel.IsDeleted = ticket.IsDeleted;

                        foreach (TicketFile item in ticket.TicketFiles)
                        {
                            ticketViewModel.TicketFileViewModels.Add(new TicketFileViewModel()
                            {
                                TicketID = ticket.TicketID,
                                FileName = item.FileName,
                                FileForTOrN = item.FileForTOrN,
                                RefID = item.RefID,
                                CreatedBy = item.CreatedBy,
                                CreationDate = item.CreationDate,
                                TicketViewModel = ticketViewModel
                            });
                        }

                        foreach (TicketNote item in ticket.TicketNotes)
                        {
                            ticketViewModel.TicketNoteViewModels.Add(new TicketNoteViewModel()
                            {
                                TicketID = ticket.TicketID,
                                Note = item.Note,
                                CreatedBy = item.CreatedBy,
                                CreationDate = item.CreationDate,
                                TicketViewModel = ticketViewModel
                            });
                        }
                    }
                }

                ViewBag.Errors = errors;
            }
            catch (Exception ex)
            {
                HomeController.LogError(ex, HttpContext.Server.MapPath("~/Error_Log.txt"));
            }

            return View(ticketViewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UpdateTicket(TicketViewModel model)
        {
            StringBuilder errors = new StringBuilder();
            Guid id = model.TicketID;
            bool isSuccessful = false;
            bool sendEmailAlert = false;
            GeneralSetting generalSetting = null;

            try
            {
                #region validate inputs                
                if (model.TicketID == null)
                    errors.AppendLine("Ticket ID is required.");
                if (string.IsNullOrWhiteSpace(model.Description))
                    errors.AppendLine("Description is required.");
                if (model.FileAttachments != null)
                {
                    string[] unsupportedFileTypes = new[] { "exe" };
                }
                #endregion

                if (errors.Length > 0)
                {
                    return RedirectToAction("TicketDetails", new RouteValueDictionary(new { controller = "Home", action = "TicketDetails", id = id, errors = errors.ToString() }));
                }
                else
                {
                    // sanitize data
                    model.Description = SecurityHelper.Sanitize(model.Description);

                    #region save data
                    using (TransactionScope transactionScope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
                    {
                        using (HelpDeskEntities dataContext = new HelpDeskEntities())
                        {
                            Ticket ticket = await dataContext.Tickets.FirstOrDefaultAsync(p => p.TicketID.Equals(model.TicketID));

                            if (ticket == null)
                            {
                                errors.AppendLine("Unknown ticket details");
                            }

                            if (errors.Length == 0)
                            {
                                model.Subject = ticket.Subject;
                                model.OwnerEmail = ticket.OwnerEmail;

                                ticket.Status = model.Status;
                                ticket.ModifiedBy = User.Identity.Name;

                                dataContext.Entry(ticket).State = EntityState.Modified;

                                if (await dataContext.SaveChangesAsync() > 0)
                                {
                                    Guid ticketNoteID = Guid.NewGuid();

                                    #region ticket note
                                    TicketNote ticketNote = new TicketNote()
                                    {
                                        TicketNoteID = ticketNoteID,
                                        TicketID = model.TicketID,
                                        Note = model.Description,
                                        CreatedBy = User.Identity.Name,
                                        CreationDate = DateTime.Now,
                                        ModifiedBy = User.Identity.Name,
                                        ModifiedDate = DateTime.Now,
                                        IsDeleted = false
                                    };

                                    dataContext.TicketNotes.Add(ticketNote);

                                    if (await dataContext.SaveChangesAsync() > 0)
                                    {

                                        #region ticket files
                                        if (model.FileAttachments != null)
                                        {
                                            if (model.FileAttachments.Length > 0)
                                            {
                                                string targetPath = HttpContext.Server.MapPath(this.fileAttachmentPath);

                                                if (model.FileAttachments[0] != null)
                                                {
                                                    List<TicketFile> ticketFiles = new List<TicketFile>();

                                                    foreach (var file in model.FileAttachments)
                                                    {
                                                        string fileName = Path.Combine(targetPath, file.FileName);
                                                        file.SaveAs(fileName);

                                                        ticketFiles.Add(new TicketFile()
                                                        {
                                                            TicketFileID = Guid.NewGuid(),
                                                            TicketID = ticket.TicketID,
                                                            FileName = file.FileName,
                                                            FileForTOrN = "N",
                                                            RefID = ticketNoteID,
                                                            CreatedBy = User.Identity.Name,
                                                            CreationDate = DateTime.Now,
                                                            ModifiedBy = User.Identity.Name,
                                                            ModifiedDate = DateTime.Now,
                                                            IsDeleted = false
                                                        });
                                                    }

                                                    dataContext.TicketFiles.AddRange(ticketFiles);

                                                    await dataContext.SaveChangesAsync();
                                                }
                                            }
                                        }
                                        #endregion
                                    }
                                    #endregion

                                    // trigger e-mail [optional]
                                    generalSetting = await dataContext.GeneralSettings.FirstOrDefaultAsync();
                                    if (generalSetting != null)
                                    {
                                        if (generalSetting.EnableEmailAlert && !string.IsNullOrWhiteSpace(generalSetting.RecipientEmails))
                                        {
                                            sendEmailAlert = true;
                                        }
                                    }

                                    transactionScope.Complete();
                                    isSuccessful = true;
                                }
                                else
                                {
                                    errors.AppendLine("Unknow error occured.");
                                }
                            }
                        }
                    }
                    #endregion                   
                }
            }
            catch (DbEntityValidationException ex)
            {
                isSuccessful = false;
                HomeController.LogError(ex, HttpContext.Server.MapPath("~/Error_Log.txt"));
            }
            catch (Exception ex)
            {
                isSuccessful = false;
                errors.AppendLine(ex.Message);
                HomeController.LogError(ex, HttpContext.Server.MapPath("~/Error_Log.txt"));
            }

            if (isSuccessful)
            {
                if (sendEmailAlert)
                    if (HomeController.IsMember(User.Identity.Name, ConfigurationManager.AppSettings["ADAG"].ToString()))
                        SendEmail("ICT HelpDesk Re: [" + model.TicketNo + "] " + model.Subject, model.Description, model.OwnerEmail, id.ToString(), model.FileAttachments);
                    else
                        SendEmail("ICT HelpDesk Re: [" + model.TicketNo + "] " + model.Subject, model.Description, generalSetting.RecipientEmails, id.ToString(), model.FileAttachments);

                return RedirectToAction("TicketDetails", new RouteValueDictionary(new { controller = "Home", action = "TicketDetails", id = id }));
            }

            return RedirectToAction("TicketDetails", new RouteValueDictionary(new { controller = "Home", action = "TicketDetails", id = id, errors = errors.ToString() }));
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }

        public static void LogError(Exception err, string path)
        {
            try
            {
                #region error details
                HttpException error = err as HttpException;
                Exception lastError = error;

                if (lastError.InnerException != null)
                    lastError = lastError.InnerException;

                string errorDateTime = string.Format("{0:dd/MM/yyyy hh:mm:ss}", DateTime.Now);
                string errorType = lastError.GetType().ToString();
                string errorMessage = lastError.Message;
                string errorStackTrace = lastError.StackTrace;
                string errorSource = lastError.Source;
                string errorMethod = lastError.TargetSite.Name;

                StringBuilder exception = new StringBuilder();

                exception.AppendLine(string.Format("Date/Time: {0}", errorDateTime));
                exception.AppendLine(string.Format("Error Type: {0}", errorType));
                exception.AppendLine(string.Format("Methord: {0}", errorMethod));
                exception.AppendLine(string.Format("Source: {0}", errorSource));
                exception.AppendLine(string.Format("Message: {0}", errorMessage));
                exception.AppendLine(string.Format("Stack Trace: {0}", errorStackTrace));
                #endregion

                #region save the error in a file
                using (FileStream fs = new FileStream(path, FileMode.Append, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    StreamWriter sr = new StreamWriter(fs);

                    sr.WriteLine(exception);
                }
                #endregion                
            }
            catch (Exception ex)
            {
                string error = ex.Message;
            }
        }
    }
}