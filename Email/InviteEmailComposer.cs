using System.Text.Encodings.Web;
using CadastraAI.API.Models;

namespace CadastraAI.API.Email;

public static class InviteEmailComposer
{
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Default;

    public static EmailMessage Compose(
        string to,
        string empresaNome,
        string inviterName,
        MembershipRole role,
        string inviteUrl,
        DateTime expiresAt)
    {
        var roleLabel = role switch
        {
            MembershipRole.Owner => "Dono",
            MembershipRole.Admin => "Administrador",
            _ => "Membro",
        };

        var rolePerms = role switch
        {
            MembershipRole.Admin =>
                "Você poderá cadastrar leads, consultas e tratamentos, além de convidar e gerenciar a equipe.",
            MembershipRole.Member =>
                "Você poderá cadastrar leads, consultas, tratamentos e ver os indicadores da empresa.",
            _ =>
                "Você terá acesso total à empresa.",
        };

        var safeEmpresa = Encoder.Encode(empresaNome);
        var safeInviter = Encoder.Encode(inviterName);
        var safeRole = Encoder.Encode(roleLabel);
        var safePerms = Encoder.Encode(rolePerms);
        var safeUrl = Encoder.Encode(inviteUrl);
        var safeExpires = expiresAt.ToString("dd/MM/yyyy");

        var html = $$"""
        <!DOCTYPE html>
        <html lang="pt-BR">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1" />
          <title>Convite — {{safeEmpresa}}</title>
        </head>
        <body style="margin:0;padding:0;background:#091420;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:#e2e8f0;">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#091420;padding:32px 16px;">
            <tr>
              <td align="center">
                <table role="presentation" width="560" cellpadding="0" cellspacing="0" style="max-width:560px;width:100%;background:#0d1c2a;border:1px solid rgba(34,211,238,0.18);border-radius:16px;overflow:hidden;">
                  <tr>
                    <td style="padding:28px 32px;border-bottom:1px solid rgba(34,211,238,0.14);">
                      <div style="font-size:11px;letter-spacing:0.22em;text-transform:uppercase;color:#67e8f9;font-weight:600;">cadastra.ai</div>
                      <div style="font-size:22px;color:#f8fafc;font-weight:700;margin-top:6px;letter-spacing:-0.01em;">Você foi convidado</div>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:24px 32px 8px 32px;">
                      <p style="margin:0 0 14px 0;font-size:15px;line-height:1.55;color:#e2e8f0;">
                        <strong style="color:#f8fafc;">{{safeInviter}}</strong> convidou você para participar de
                        <strong style="color:#67e8f9;">{{safeEmpresa}}</strong> como
                        <strong style="color:#67e8f9;">{{safeRole}}</strong>.
                      </p>
                      <p style="margin:0 0 18px 0;font-size:14px;line-height:1.6;color:#94a3b8;">
                        {{safePerms}}
                      </p>
                    </td>
                  </tr>
                  <tr>
                    <td align="center" style="padding:8px 32px 28px 32px;">
                      <a href="{{safeUrl}}"
                        style="display:inline-block;padding:14px 28px;border-radius:10px;background:linear-gradient(135deg,#22d3ee,#38bdf8);color:#081420;text-decoration:none;font-weight:700;font-size:15px;letter-spacing:-0.01em;box-shadow:0 0 30px -10px rgba(34,211,238,0.7);">
                        Aceitar convite
                      </a>
                      <div style="margin-top:14px;font-size:12px;color:#64748b;">
                        Este convite expira em {{safeExpires}}.
                      </div>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:0 32px 28px 32px;">
                      <div style="padding:14px;border-radius:10px;background:rgba(34,211,238,0.06);border:1px solid rgba(34,211,238,0.18);">
                        <div style="font-size:11px;letter-spacing:0.18em;text-transform:uppercase;color:#94a3b8;font-weight:600;margin-bottom:6px;">
                          Link manual
                        </div>
                        <div style="font-size:12px;word-break:break-all;color:#67e8f9;">
                          {{safeUrl}}
                        </div>
                      </div>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:18px 32px;border-top:1px solid rgba(34,211,238,0.10);font-size:11px;color:#64748b;">
                      Se você não esperava este convite, pode ignorar este e-mail com segurança.
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;

        var text = $"""
        Você foi convidado para {empresaNome} como {roleLabel}.

        {inviterName} é quem convidou você.
        {rolePerms}

        Para aceitar, abra o link:
        {inviteUrl}

        Este convite expira em {safeExpires}.
        """;

        return new EmailMessage(
            to,
            $"Convite para {empresaNome}",
            html,
            text);
    }
}
