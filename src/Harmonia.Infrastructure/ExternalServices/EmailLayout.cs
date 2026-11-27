using System.Net;

namespace Harmonia.Infrastructure.ExternalServices;

/// <summary>
/// Wraps every outgoing email body in the Harmonia design-system frame (bookcloth head over the church photo,
/// gold hairline, white sheet on warm paper, chant-staff footer). Table layout with inline styles because
/// Gmail and Outlook drop most CSS; images come from <see cref="BrevoOptions.AssetBaseUrl"/> since mail clients
/// block data URIs and SVG.
/// </summary>
public static class EmailLayout
{
    public static string Wrap(string assetBaseUrl, string subject, string bodyHtml)
    {
        var assets = assetBaseUrl.TrimEnd('/');
        var title = WebUtility.HtmlEncode(subject);

        return $$"""
<!DOCTYPE html>
<html lang="vi">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{{title}}</title>
<link href="https://fonts.googleapis.com/css2?family=Be+Vietnam+Pro:wght@400;500;600&family=Cormorant+Garamond:wght@600&display=swap" rel="stylesheet">
<style>
  .hm-body p { margin: 0 0 16px; }
  .hm-body p:last-child { margin-bottom: 0; }
  .hm-body a { color: #8e3b4b; }
  .hm-body a.btn { display: inline-block; background: #8e3b4b; color: #ffffff !important; text-decoration: none;
    font-weight: 500; font-size: 15px; line-height: 40px; padding: 0 24px; border-radius: 6px; }
  .hm-body a.btn:hover { background: #762e3d; }
</style>
</head>
<body style="margin:0;padding:0;background:#f5f0e8;">
<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" bgcolor="#f5f0e8" style="background:#f5f0e8;">
<tr><td align="center" style="padding:32px 16px;">
  <table role="presentation" width="600" cellpadding="0" cellspacing="0" border="0" style="width:100%;max-width:600px;font-family:'Be Vietnam Pro',Manrope,'Segoe UI',Arial,sans-serif;">
    <tr><td bgcolor="#4a1d27" background="{{assets}}/church-light.jpg" style="background:#4a1d27 url('{{assets}}/church-light.jpg') center/cover no-repeat;border-radius:8px 8px 0 0;">
      <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
        <tr><td style="background:rgba(74,29,39,0.82);border-radius:8px 8px 0 0;padding:28px 32px;">
          <table role="presentation" cellpadding="0" cellspacing="0" border="0"><tr>
            <td style="vertical-align:middle;padding-right:12px;"><img src="{{assets}}/harmonia-mark.png" width="40" height="40" alt="Harmonia" style="display:block;border:0;"></td>
            <td style="vertical-align:middle;font-family:'Cormorant Garamond',Georgia,'Times New Roman',serif;font-weight:600;font-size:28px;line-height:1.2;color:#ffffff;">Harmonia</td>
          </tr></table>
        </td></tr>
      </table>
    </td></tr>
    <tr><td height="2" bgcolor="#c5a46d" style="height:2px;line-height:2px;font-size:0;background:#c5a46d;">&nbsp;</td></tr>
    <tr><td bgcolor="#ffffff" style="background:#ffffff;border-left:1px solid #e6e1d9;border-right:1px solid #e6e1d9;padding:32px;">
      <h1 style="margin:0 0 20px;font-size:20px;line-height:1.4;font-weight:600;color:#1e1a1c;">{{title}}</h1>
      <div class="hm-body" style="font-size:15px;line-height:1.6;color:#3f3639;">{{bodyHtml}}</div>
    </td></tr>
    <tr><td bgcolor="#f5f0e8" style="background:#f5f0e8;border:1px solid #e6e1d9;border-top:0;padding:20px 32px 12px;font-size:13px;line-height:1.6;color:#615558;">
      Email tự động từ Harmonia, vui lòng không trả lời.
    </td></tr>
    <tr><td style="font-size:0;line-height:0;"><img src="{{assets}}/antiphonary-ground.png" width="600" height="38" alt="" style="display:block;width:100%;height:auto;border:0;"></td></tr>
  </table>
</td></tr>
</table>
</body>
</html>
""";
    }
}
