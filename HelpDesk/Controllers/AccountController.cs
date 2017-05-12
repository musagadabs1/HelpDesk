using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;
using HelpDesk.Models;
using System.Web.Security;
using System.DirectoryServices.AccountManagement;
using System.Configuration;
using System.DirectoryServices;

namespace HelpDesk.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        //
        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        //
        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            using (PrincipalContext pc = new PrincipalContext(ContextType.Domain, ConfigurationManager.AppSettings["ADIP"].ToString()))
            {
                bool isValid = pc.ValidateCredentials(model.UserName, model.Password);

                if (isValid)
                {
                    DirectoryEntry directoryEntry = new DirectoryEntry(ConfigurationManager.ConnectionStrings["ADConnectionString"].ConnectionString, model.UserName, model.Password);
                    object nativeObject = directoryEntry.NativeObject;
                    var userEmail = new DirectorySearcher(directoryEntry)
                    {
                        Filter = "samaccountname=" + model.UserName,
                        PropertiesToLoad = { "mail" }
                    }.FindOne().Properties["mail"][0];

                    FormsAuthentication.SetAuthCookie(model.UserName, false);

                    Session["userEmail"] = userEmail;


                    if (Url.IsLocalUrl(returnUrl) && returnUrl.Length > 1
                        && returnUrl.StartsWith("/") && !returnUrl.StartsWith("//")
                        && !returnUrl.StartsWith("/\\"))
                    {
                        return Redirect(returnUrl);
                    }
                    else
                    {
                        if (HomeController.IsMember(model.UserName, ConfigurationManager.AppSettings["ADAG"].ToString()))
                        {
                            return RedirectToAction("Tickets", "Home", new { status = "" });
                        }
                        else
                        {
                            return RedirectToAction("Index", "Home");
                        }
                    }
                }
                else
                {
                    ModelState.AddModelError("", "The user name or password provided is incorrect");
                }
            }
            
            /*if (Membership.ValidateUser(model.UserName, model.Password))
            {
                FormsAuthentication.SetAuthCookie(model.UserName, false);
                if (Url.IsLocalUrl(returnUrl) && returnUrl.Length > 1 
                    && returnUrl.StartsWith("/") && !returnUrl.StartsWith("//") 
                    && !returnUrl.StartsWith("/\\"))
                {
                    return Redirect(returnUrl);
                }
                else
                {
                    return RedirectToAction("Index", "Home");
                }
            }
            else
            {
                ModelState.AddModelError("", "The user name or password provided is incorrect");
            }*/

            return View(model);
        }

        //
        // POST: /Account/LogOff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            FormsAuthentication.SignOut();
            return RedirectToAction("Index", "Home");
        }
    }
}