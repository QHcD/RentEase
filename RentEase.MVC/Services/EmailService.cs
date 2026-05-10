using System.Net;
using System.Net.Mail;

namespace PropertyLeasing.MVC.Services;

public class EmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    // ── Send Password Reset Code ───────────────────────────────────────────
    public async Task SendPasswordResetAsync(string toEmail, string toName, string code)
    {
        string subject = "RentEase — Password Reset Code";
        string body    = BuildResetEmail(toName, code);
        await SendAsync(toEmail, subject, body);
    }

    // ── Maintenance Submitted ──────────────────────────────────────────────
    public async Task SendMaintenanceSubmittedAsync(string toEmail, string toName,
        string ticketNumber, string title, string requestType,
        string priority, string unitNumber, string propertyName)
    {
        string subject = $"RentEase — Maintenance Request Received ({ticketNumber})";
        string body    = BuildMaintenanceEmail(toName, ticketNumber, title, requestType,
            priority, unitNumber, propertyName,
            status: "Submitted", statusColor: "#1a73e8",
            message: "Your maintenance request has been successfully received and is now in our queue. Our team will review it shortly and assign the appropriate technician.");
        await SendAsync(toEmail, subject, body);
    }

    // ── Maintenance Status Changed ─────────────────────────────────────────
    public async Task SendMaintenanceStatusChangedAsync(string toEmail, string toName,
        string ticketNumber, string title, string unitNumber,
        string newStatus, string? notes)
    {
        string subject = $"RentEase — Maintenance Update ({ticketNumber})";

        var (color, msg) = newStatus switch
        {
            "Assigned"   => ("#0288d1", "Your request has been assigned to our maintenance team. A technician will be visiting your unit soon."),
            "InProgress" => ("#e65100", "Our technician is currently working on your maintenance request. You will be notified once it is resolved."),
            "Resolved"   => ("#2e7d32", "Great news! Your maintenance request has been resolved. Please let us know if the issue persists."),
            "Closed"     => ("#546e7a", "Your maintenance request has been closed. Thank you for your patience."),
            _            => ("#555555", $"Your maintenance request status has been updated to: {newStatus}.")
        };

        string icon = newStatus switch
        {
            "Assigned"   => "🔧",
            "InProgress" => "⚙️",
            "Resolved"   => "✅",
            "Closed"     => "🔒",
            _            => "📋"
        };

        string noteSection = !string.IsNullOrWhiteSpace(notes)
            ? $"<tr><td style='color:#888;'>Notes</td><td><em>{notes}</em></td></tr>"
            : "";

        string body = $"""
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="UTF-8"/></head>
        <body style="margin:0;padding:0;background:#f4f6f9;font-family:'Segoe UI',Arial,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f6f9;padding:40px 0;">
            <tr><td align="center">
              <table width="600" cellpadding="0" cellspacing="0"
                     style="background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,0.08);">
                <tr><td style="background:linear-gradient(135deg,#1a73e8,#0d47a1);padding:36px 40px;text-align:center;">
                  <h1 style="margin:0;color:#fff;font-size:28px;font-weight:700;">🏠 RentEase</h1>
                  <p style="margin:6px 0 0;color:#bbdefb;font-size:14px;">Property Leasing & Management Platform</p>
                </td></tr>
                <tr><td style="background:{color};padding:16px 40px;text-align:center;">
                  <p style="margin:0;color:#fff;font-size:20px;font-weight:700;">{icon} Maintenance {newStatus}</p>
                </td></tr>
                <tr><td style="padding:40px;">
                  <p style="margin:0 0 8px;color:#555;font-size:15px;">Hello, <strong>{toName}</strong></p>
                  <p style="margin:0 0 28px;color:#555;font-size:15px;line-height:1.7;">{msg}</p>
                  <table width="100%" cellpadding="4" cellspacing="0"
                         style="background:#f8f9fa;border-radius:10px;padding:20px;margin-bottom:24px;font-size:14px;color:#555;line-height:2;">
                    <tr><td style="color:#888;width:40%;">Ticket Number</td><td><strong style="color:{color};">{ticketNumber}</strong></td></tr>
                    <tr><td style="color:#888;">Issue</td><td><strong>{title}</strong></td></tr>
                    <tr><td style="color:#888;">Unit</td><td><strong>{unitNumber}</strong></td></tr>
                    <tr><td style="color:#888;">Status</td><td><strong style="color:{color};">{newStatus}</strong></td></tr>
                    {noteSection}
                  </table>
                  <p style="margin:0 0 28px;color:#555;font-size:14px;">Track your request anytime using ticket number <strong>{ticketNumber}</strong> on the RentEase portal.</p>
                  <p style="margin:0;color:#999;font-size:13px;">Best regards,<br/><strong style="color:#333;">The RentEase Team</strong></p>
                </td></tr>
                <tr><td style="background:#f8f9fa;padding:20px 40px;text-align:center;border-top:1px solid #e9ecef;">
                  <p style="margin:0;color:#aaa;font-size:12px;">© 2026 RentEase — Property Leasing & Management Platform<br/>This is an automated message, please do not reply.</p>
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
        await SendAsync(toEmail, subject, body);
    }

    // ── Maintenance Email Template ─────────────────────────────────────────
    private static string BuildMaintenanceEmail(string name, string ticketNumber,
        string title, string requestType, string priority,
        string unitNumber, string propertyName,
        string status, string statusColor, string message)
    {
        string priorityColor = priority switch
        {
            "Urgent" => "#c62828",
            "High"   => "#e65100",
            "Medium" => "#1565c0",
            _        => "#555"
        };

        string typeIcon = requestType switch
        {
            "Electrical"  => "⚡",
            "AC / HVAC"   => "❄️",
            "Plumbing"    => "🔧",
            "Lock / Door" => "🚪",
            "Lighting"    => "💡",
            "Windows"     => "🪟",
            "Cleaning"    => "🧹",
            _             => "🔨"
        };

        return $"""
        <!DOCTYPE html>
        <html lang="en">
        <head><meta charset="UTF-8"/></head>
        <body style="margin:0;padding:0;background:#f4f6f9;font-family:'Segoe UI',Arial,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f6f9;padding:40px 0;">
            <tr><td align="center">
              <table width="600" cellpadding="0" cellspacing="0"
                     style="background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 4px 20px rgba(0,0,0,0.08);">
                <tr><td style="background:linear-gradient(135deg,#1a73e8,#0d47a1);padding:36px 40px;text-align:center;">
                  <h1 style="margin:0;color:#fff;font-size:28px;font-weight:700;">🏠 RentEase</h1>
                  <p style="margin:6px 0 0;color:#bbdefb;font-size:14px;">Property Leasing & Management Platform</p>
                </td></tr>
                <tr><td style="background:{statusColor};padding:16px 40px;text-align:center;">
                  <p style="margin:0;color:#fff;font-size:20px;font-weight:700;">🔧 Maintenance Request Received</p>
                </td></tr>
                <tr><td style="padding:40px;">
                  <p style="margin:0 0 8px;color:#555;font-size:15px;">Hello, <strong>{name}</strong></p>
                  <p style="margin:0 0 28px;color:#555;font-size:15px;line-height:1.7;">{message}</p>

                  <table width="100%" cellpadding="0" cellspacing="0"
                         style="background:#e8f5e9;border:1px solid #c8e6c9;border-radius:10px;padding:20px;margin-bottom:20px;">
                    <tr><td style="font-size:14px;color:#2e7d32;">
                      <strong>🎫 Your Ticket Number</strong><br/>
                      <span style="font-size:28px;font-weight:800;letter-spacing:4px;font-family:'Courier New',monospace;color:#1b5e20;">{ticketNumber}</span><br/>
                      <small style="color:#555;">Keep this number to track your request</small>
                    </td></tr>
                  </table>

                  <table width="100%" cellpadding="4" cellspacing="0"
                         style="background:#f8f9fa;border-radius:10px;padding:20px;margin-bottom:24px;font-size:14px;color:#555;line-height:2;">
                    <tr><td style="color:#888;width:40%;">Issue</td><td><strong>{title}</strong></td></tr>
                    <tr><td style="color:#888;">Type</td><td>{typeIcon} <strong>{requestType}</strong></td></tr>
                    <tr><td style="color:#888;">Priority</td><td><strong style="color:{priorityColor};">{priority}</strong></td></tr>
                    <tr><td style="color:#888;">Unit</td><td><strong>{unitNumber}</strong></td></tr>
                    <tr><td style="color:#888;">Property</td><td><strong>{propertyName}</strong></td></tr>
                    <tr><td style="color:#888;">Status</td><td><strong style="color:{statusColor};">{status}</strong></td></tr>
                  </table>

                  <p style="margin:0;color:#999;font-size:13px;">Best regards,<br/><strong style="color:#333;">The RentEase Team</strong></p>
                </td></tr>
                <tr><td style="background:#f8f9fa;padding:20px 40px;text-align:center;border-top:1px solid #e9ecef;">
                  <p style="margin:0;color:#aaa;font-size:12px;">© 2026 RentEase — Property Leasing & Management Platform<br/>This is an automated message, please do not reply.</p>
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
    }

    // ── Application Submitted ──────────────────────────────────────────────
    public async Task SendApplicationSubmittedAsync(string toEmail, string toName,
        string unitNumber, string propertyName, int applicationId)
    {
        string subject = "RentEase — Application Received";
        string body    = BuildApplicationEmail(toName, unitNumber, propertyName, applicationId,
            status:  "Pending",
            title:   "Application Received",
            message: "Thank you! We have successfully received your lease application and it is now under review by our team.",
            color:   "#1a73e8",
            icon:    "📋");
        await SendAsync(toEmail, subject, body);
    }

    // ── Application Screening ──────────────────────────────────────────────
    public async Task SendApplicationScreeningAsync(string toEmail, string toName,
        string unitNumber, string propertyName, int applicationId)
    {
        string subject = "RentEase — Application Under Screening";
        string body    = BuildApplicationEmail(toName, unitNumber, propertyName, applicationId,
            status:  "Screening",
            title:   "Application Under Screening",
            message: "Good news! Your lease application is now being actively reviewed and screened by our property management team. We will get back to you shortly.",
            color:   "#0288d1",
            icon:    "🔍");
        await SendAsync(toEmail, subject, body);
    }

    // ── Application Approved ───────────────────────────────────────────────
    public async Task SendApplicationApprovedAsync(string toEmail, string toName,
        string unitNumber, string propertyName, int applicationId)
    {
        string subject = "RentEase — Application Approved 🎉";
        string body    = BuildApplicationEmail(toName, unitNumber, propertyName, applicationId,
            status:  "Approved",
            title:   "Application Approved!",
            message: "Congratulations! Your lease application has been approved. Please log in to RentEase and complete your payment to activate your lease.",
            color:   "#2e7d32",
            icon:    "✅");
        await SendAsync(toEmail, subject, body);
    }

    // ── Application Rejected ───────────────────────────────────────────────
    public async Task SendApplicationRejectedAsync(string toEmail, string toName,
        string unitNumber, string propertyName, int applicationId)
    {
        string subject = "RentEase — Application Update";
        string body    = BuildApplicationEmail(toName, unitNumber, propertyName, applicationId,
            status:  "Rejected",
            title:   "Application Not Approved",
            message: "We regret to inform you that your lease application was not approved at this time. You are welcome to apply for other available units on our platform.",
            color:   "#c62828",
            icon:    "❌");
        await SendAsync(toEmail, subject, body);
    }

    // ── Core Send Method ───────────────────────────────────────────────────
    private async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        var cfg      = _config.GetSection("EmailSettings");
        var host     = cfg["SmtpHost"]    ?? "smtp.gmail.com";
        var port     = int.Parse(cfg["SmtpPort"] ?? "587");
        var sender   = cfg["SenderEmail"] ?? "";
        var name     = cfg["SenderName"]  ?? "RentEase";
        var password = cfg["AppPassword"] ?? "";

        using var client = new SmtpClient(host, port)
        {
            Credentials    = new NetworkCredential(sender, password),
            EnableSsl      = true,
            DeliveryMethod = SmtpDeliveryMethod.Network
        };

        using var message = new MailMessage
        {
            From       = new MailAddress(sender, name),
            Subject    = subject,
            Body       = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(toEmail));

        await client.SendMailAsync(message);
    }

    // ── Payment Confirmation ───────────────────────────────────────────────
    public async Task SendPaymentConfirmationAsync(string toEmail, string toName,
        string unitNumber, string propertyName,
        string planType, decimal amountPaid, DateTime paidOn,
        string leaseStatus, DateTime leaseStart, DateTime leaseEnd)
    {
        string subject = "RentEase — Payment Confirmed ✅";
        string body    = BuildPaymentEmail(toName, unitNumber, propertyName,
            planType, amountPaid, paidOn, leaseStatus, leaseStart, leaseEnd);
        await SendAsync(toEmail, subject, body);
    }

    // ── Payment Email Template ─────────────────────────────────────────────
    private static string BuildPaymentEmail(string name, string unitNumber, string propertyName,
        string planType, decimal amountPaid, DateTime paidOn,
        string leaseStatus, DateTime leaseStart, DateTime leaseEnd)
    {
        string statusColor = leaseStatus == "Active" ? "#2e7d32" : "#1a73e8";
        string planDesc    = planType == "Full"
            ? "Full Payment (entire lease period)"
            : "Monthly Installment Plan (first installment)";

        return $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="UTF-8"/>
          <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
          <title>Payment Confirmed</title>
        </head>
        <body style="margin:0;padding:0;background:#f4f6f9;font-family:'Segoe UI',Arial,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f6f9;padding:40px 0;">
            <tr><td align="center">
              <table width="600" cellpadding="0" cellspacing="0"
                     style="background:#ffffff;border-radius:12px;overflow:hidden;
                            box-shadow:0 4px 20px rgba(0,0,0,0.08);">

                <!-- Header -->
                <tr>
                  <td style="background:linear-gradient(135deg,#1a73e8,#0d47a1);
                             padding:36px 40px;text-align:center;">
                    <h1 style="margin:0;color:#ffffff;font-size:28px;font-weight:700;">
                      🏠 RentEase
                    </h1>
                    <p style="margin:6px 0 0;color:#bbdefb;font-size:14px;">
                      Property Leasing & Management Platform
                    </p>
                  </td>
                </tr>

                <!-- Status Banner -->
                <tr>
                  <td style="background:#2e7d32;padding:16px 40px;text-align:center;">
                    <p style="margin:0;color:#ffffff;font-size:20px;font-weight:700;">
                      ✅ Payment Confirmed
                    </p>
                  </td>
                </tr>

                <!-- Body -->
                <tr>
                  <td style="padding:40px;">
                    <p style="margin:0 0 8px;color:#555;font-size:15px;">
                      Hello, <strong>{name}</strong>
                    </p>
                    <p style="margin:0 0 28px;color:#555;font-size:15px;line-height:1.7;">
                      Your payment has been successfully processed and your lease is now
                      <strong style="color:{statusColor};">{leaseStatus}</strong>.
                      Thank you for choosing RentEase!
                    </p>

                    <!-- Receipt Box -->
                    <table width="100%" cellpadding="0" cellspacing="0"
                           style="background:#f0f7f0;border:1px solid #c8e6c9;
                                  border-radius:10px;padding:24px;margin-bottom:24px;">
                      <tr>
                        <td>
                          <p style="margin:0 0 14px;color:#2e7d32;font-size:16px;font-weight:700;">
                            🧾 Payment Receipt
                          </p>
                          <table width="100%" cellpadding="4" cellspacing="0"
                                 style="font-size:14px;color:#555;">
                            <tr>
                              <td style="width:45%;color:#888;">Amount Paid</td>
                              <td><strong style="color:#2e7d32;font-size:18px;">BD {amountPaid:N2}</strong></td>
                            </tr>
                            <tr>
                              <td style="color:#888;">Payment Plan</td>
                              <td><strong>{planDesc}</strong></td>
                            </tr>
                            <tr>
                              <td style="color:#888;">Paid On</td>
                              <td><strong>{paidOn:dd MMM yyyy, HH:mm}</strong></td>
                            </tr>
                          </table>
                        </td>
                      </tr>
                    </table>

                    <!-- Lease Details -->
                    <table width="100%" cellpadding="0" cellspacing="0"
                           style="background:#f8f9fa;border-radius:10px;
                                  padding:20px;margin-bottom:28px;">
                      <tr>
                        <td style="color:#555;font-size:14px;line-height:2;">
                          <strong style="color:#333;">Lease Details</strong><br/>
                          🏠 Unit: <strong>{unitNumber}</strong><br/>
                          🏢 Property: <strong>{propertyName}</strong><br/>
                          📅 Start Date: <strong>{leaseStart:dd MMM yyyy}</strong><br/>
                          📅 End Date: <strong>{leaseEnd:dd MMM yyyy}</strong><br/>
                          📌 Lease Status: <strong style="color:{statusColor};">{leaseStatus}</strong>
                        </td>
                      </tr>
                    </table>

                    <p style="margin:0 0 28px;color:#555;font-size:14px;line-height:1.6;">
                      You can view your lease details and payment schedule anytime by logging
                      into <strong>RentEase</strong> and visiting <em>My Applications & Leases</em>.
                    </p>

                    <p style="margin:0;color:#999;font-size:13px;">
                      Best regards,<br/>
                      <strong style="color:#333;">The RentEase Team</strong>
                    </p>
                  </td>
                </tr>

                <!-- Footer -->
                <tr>
                  <td style="background:#f8f9fa;padding:20px 40px;text-align:center;
                             border-top:1px solid #e9ecef;">
                    <p style="margin:0;color:#aaa;font-size:12px;">
                      © 2026 RentEase — Property Leasing & Management Platform<br/>
                      This is an automated message, please do not reply.
                    </p>
                  </td>
                </tr>

              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
    }

    // ── Application Email Template ─────────────────────────────────────────
    private static string BuildApplicationEmail(string name, string unitNumber,
        string propertyName, int applicationId,
        string status, string title, string message, string color, string icon)
    {
        return $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="UTF-8"/>
          <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
          <title>{title}</title>
        </head>
        <body style="margin:0;padding:0;background:#f4f6f9;font-family:'Segoe UI',Arial,sans-serif;">
          <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f6f9;padding:40px 0;">
            <tr><td align="center">
              <table width="600" cellpadding="0" cellspacing="0"
                     style="background:#ffffff;border-radius:12px;overflow:hidden;
                            box-shadow:0 4px 20px rgba(0,0,0,0.08);">

                <!-- Header -->
                <tr>
                  <td style="background:linear-gradient(135deg,#1a73e8,#0d47a1);
                             padding:36px 40px;text-align:center;">
                    <h1 style="margin:0;color:#ffffff;font-size:28px;font-weight:700;">
                      🏠 RentEase
                    </h1>
                    <p style="margin:6px 0 0;color:#bbdefb;font-size:14px;">
                      Property Leasing & Management Platform
                    </p>
                  </td>
                </tr>

                <!-- Status Banner -->
                <tr>
                  <td style="background:{color};padding:16px 40px;text-align:center;">
                    <p style="margin:0;color:#ffffff;font-size:20px;font-weight:700;">
                      {icon} {title}
                    </p>
                  </td>
                </tr>

                <!-- Body -->
                <tr>
                  <td style="padding:40px;">
                    <p style="margin:0 0 8px;color:#555;font-size:15px;">
                      Hello, <strong>{name}</strong>
                    </p>
                    <p style="margin:0 0 28px;color:#555;font-size:15px;line-height:1.7;">
                      {message}
                    </p>

                    <!-- Application Details -->
                    <table width="100%" cellpadding="0" cellspacing="0"
                           style="background:#f8f9fa;border-radius:10px;
                                  padding:20px;margin-bottom:28px;">
                      <tr>
                        <td style="color:#555;font-size:14px;line-height:2;">
                          <strong style="color:#333;">Application Details</strong><br/>
                          🔖 Application ID: <strong>#{applicationId}</strong><br/>
                          🏠 Unit: <strong>{unitNumber}</strong><br/>
                          🏢 Property: <strong>{propertyName}</strong><br/>
                          📌 Status: <strong style="color:{color};">{status}</strong>
                        </td>
                      </tr>
                    </table>

                    <p style="margin:0 0 28px;color:#555;font-size:14px;line-height:1.6;">
                      You can track the status of your application anytime by logging into
                      <strong>RentEase</strong> and visiting <em>My Applications & Leases</em>.
                    </p>

                    <p style="margin:0;color:#999;font-size:13px;">
                      Best regards,<br/>
                      <strong style="color:#333;">The RentEase Team</strong>
                    </p>
                  </td>
                </tr>

                <!-- Footer -->
                <tr>
                  <td style="background:#f8f9fa;padding:20px 40px;text-align:center;
                             border-top:1px solid #e9ecef;">
                    <p style="margin:0;color:#aaa;font-size:12px;">
                      © 2026 RentEase — Property Leasing & Management Platform<br/>
                      This is an automated message, please do not reply.
                    </p>
                  </td>
                </tr>

              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
    }

    // ── HTML Email Template ────────────────────────────────────────────────
    private static string BuildResetEmail(string name, string code)
    {
        // Split code into two groups for readability: XXX XXX
        string codeDisplay = code.Length == 6
            ? $"{code[..3]} {code[3..]}"
            : code;

        return $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="UTF-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
          <title>Password Reset</title>
        </head>
        <body style="margin:0;padding:0;background:#f4f6f9;font-family:'Segoe UI',Arial,sans-serif;">

          <!-- Wrapper -->
          <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f6f9;padding:40px 0;">
            <tr><td align="center">
              <table width="600" cellpadding="0" cellspacing="0"
                     style="background:#ffffff;border-radius:12px;overflow:hidden;
                            box-shadow:0 4px 20px rgba(0,0,0,0.08);">

                <!-- Header -->
                <tr>
                  <td style="background:linear-gradient(135deg,#1a73e8,#0d47a1);
                             padding:36px 40px;text-align:center;">
                    <h1 style="margin:0;color:#ffffff;font-size:28px;font-weight:700;
                               letter-spacing:1px;">
                      🏠 RentEase
                    </h1>
                    <p style="margin:6px 0 0;color:#bbdefb;font-size:14px;">
                      Property Leasing & Management Platform
                    </p>
                  </td>
                </tr>

                <!-- Body -->
                <tr>
                  <td style="padding:40px;">

                    <p style="margin:0 0 8px;color:#555;font-size:15px;">Hello, <strong>{name}</strong></p>
                    <p style="margin:0 0 28px;color:#555;font-size:15px;line-height:1.6;">
                      We received a request to reset your password. Use the verification
                      code below to continue. This code is valid for <strong>15 minutes</strong>.
                    </p>

                    <!-- Code Box -->
                    <table width="100%" cellpadding="0" cellspacing="0" style="margin-bottom:28px;">
                      <tr><td align="center">
                        <div style="display:inline-block;background:#f0f4ff;border:2px dashed #1a73e8;
                                    border-radius:12px;padding:20px 48px;">
                          <p style="margin:0 0 4px;color:#888;font-size:12px;
                                    text-transform:uppercase;letter-spacing:2px;">
                            Your Reset Code
                          </p>
                          <p style="margin:0;color:#1a73e8;font-size:42px;font-weight:800;
                                    letter-spacing:8px;font-family:'Courier New',monospace;">
                            {codeDisplay}
                          </p>
                        </div>
                      </td></tr>
                    </table>

                    <!-- Steps -->
                    <table width="100%" cellpadding="0" cellspacing="0"
                           style="background:#f8f9fa;border-radius:8px;
                                  padding:20px;margin-bottom:28px;">
                      <tr>
                        <td style="color:#555;font-size:14px;line-height:1.8;">
                          <strong style="color:#333;">How to reset your password:</strong><br/>
                          1. Go back to the RentEase reset page<br/>
                          2. Enter the 6-digit code above<br/>
                          3. Set your new password
                        </td>
                      </tr>
                    </table>

                    <!-- Warning -->
                    <table width="100%" cellpadding="0" cellspacing="0"
                           style="background:#fff8e1;border-left:4px solid #ffc107;
                                  border-radius:0 8px 8px 0;padding:14px 18px;margin-bottom:28px;">
                      <tr>
                        <td style="color:#795548;font-size:13px;line-height:1.6;">
                          ⚠️ <strong>Didn't request this?</strong> You can safely ignore this email.
                          Your password will not be changed unless you enter this code.
                        </td>
                      </tr>
                    </table>

                    <p style="margin:0;color:#999;font-size:13px;">
                      Best regards,<br/>
                      <strong style="color:#333;">The RentEase Team</strong>
                    </p>
                  </td>
                </tr>

                <!-- Footer -->
                <tr>
                  <td style="background:#f8f9fa;padding:20px 40px;text-align:center;
                             border-top:1px solid #e9ecef;">
                    <p style="margin:0;color:#aaa;font-size:12px;">
                      © 2026 RentEase — Property Leasing & Management Platform<br/>
                      This is an automated message, please do not reply.
                    </p>
                  </td>
                </tr>

              </table>
            </td></tr>
          </table>

        </body>
        </html>
        """;
    }
}
