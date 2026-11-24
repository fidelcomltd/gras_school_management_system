import type { IconName } from "@/components/ui/Icon"
import { photos, type Photo } from "@/content/images"

/** Facts and lists that appear on more than one page. Copy is verbatim from the design. */

export type Service = { name: string; ages: string; summary: string; photo: Photo; objectPosition?: string }

export const services: Service[] = [
  {
    name: "Crèche",
    ages: "6 months – 2 years",
    summary: "A safe and caring environment for infants and toddlers.",
    photo: photos.toddlersLawn,
  },
  {
    name: "Preschool",
    ages: "2 – 5 years",
    summary: "Early learning through play, creativity, and foundational skills.",
    photo: photos.preschoolBlocks,
  },
  {
    name: "Primary School",
    ages: "6 – 12 years",
    summary: "Quality education focused on academic excellence, character, and leadership development.",
    photo: photos.bandMarch,
    objectPosition: "center 40%",
  },
]

export type Reason = { label: string; icon: IconName; tone: "crimson" | "royal" | "gold" }

/** "Why choose us" — the home page shows these as icon cards, About as a checklist. */
export const reasons: Reason[] = [
  { label: "Affordable and quality education", icon: "tag", tone: "crimson" },
  { label: "Experienced and caring teachers", icon: "users", tone: "royal" },
  { label: "A conducive learning atmosphere", icon: "sun", tone: "gold" },
  { label: "Strong moral and academic standards", icon: "award", tone: "crimson" },
  { label: "Low pupil-to-teacher ratio", icon: "userCheck", tone: "royal" },
  { label: "Creative and recreational activities", icon: "music", tone: "gold" },
  { label: "Arts and crafts programmes", icon: "palette", tone: "crimson" },
  { label: "Computer and digital learning", icon: "monitor", tone: "royal" },
  { label: "Adequate security and child protection", icon: "shieldCheck", tone: "gold" },
]

/** The About page words the computer item slightly differently. */
export const reasonsLong = reasons.map((r) =>
  r.label === "Computer and digital learning" ? "Computer and digital learning education" : r.label
)

export const founderQuote =
  "Her vision is to provide quality, child-centred education built on academic excellence, discipline, integrity, creativity, and Godly values."

export const founderBelief =
  "She believes that every child is uniquely gifted and deserves the opportunity to discover and develop their full potential in a safe, caring, and stimulating environment."

export const mission =
  "To build academically and morally sound individuals by providing a safe, nurturing, and homelike environment with appropriate learning resources that stimulate learning and inspire excellence."

export const vision =
  "To provide exceptional care and the highest standard of education that enables every child to reach their full potential and become a responsible, productive, and globally minded citizen."

export const coreValues = [
  { letter: "G", name: "Gratitude" },
  { letter: "R", name: "Respect" },
  { letter: "E", name: "Excellence" },
  { letter: "A", name: "Accountability" },
  { letter: "T", name: "Trustworthiness" },
  { letter: "N", name: "Nurturing" },
  { letter: "E", name: "Effectiveness" },
  { letter: "S", name: "Self-Determination" },
  { letter: "S", name: "Self-Discipline" },
] as const

export const enquiryTopics = [
  "General enquiry",
  "Crèche (6 months – 2 years)",
  "Preschool (2 – 5 years)",
  "Primary School (6 – 12 years)",
  "Result portal help",
] as const
