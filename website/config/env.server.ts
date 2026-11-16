import "server-only"
import { blank, env as publicEnv, readEmail } from "@/config/env"

/** Server-only secrets. Importing this from a client component is a build error (server-only). */

export const serverEnv = {
  resendApiKey: blank(process.env.RESEND_API_KEY),
  contactTo: readEmail("CONTACT_FORM_TO", process.env.CONTACT_FORM_TO, publicEnv.email),
  contactFrom: blank(process.env.CONTACT_FORM_FROM) ?? "Golden Royal Ark School Website <website@goldenroyalark.com>",
} as const
