import { aboutSections, type AboutSectionId } from "@/config/routes"
import type { IconName } from "@/components/ui/Icon"
import { photos } from "@/content/images"

/** About page copy, verbatim from the "About — Desktop" and "About — Mobile" artboards. */

export const aboutHero = {
  breadcrumb: "About us",
  title: "About Golden Royal Ark School",
  text: "A nurturing and child-centred learning community, established in 2018 with a vision to provide quality education in a safe, caring and inspiring environment.",
  mobileText:
    "A nurturing and child-centred learning community, established in 2018 to provide quality education in a safe, caring and inspiring environment.",
  pills: ["Crèche", "Preschool", "Primary School"],
  badge: { title: "Est. 2018", subtitle: "Every child matters" },
  photos: { main: photos.schoolCelebration, inset: photos.morningAssembly },
}

export const aboutNav: { id: AboutSectionId; label: string; mobileLabel: string }[] = [
  { id: aboutSections.story, label: "Our story", mobileLabel: "Our story" },
  { id: aboutSections.passion, label: "What drives us", mobileLabel: "What drives us" },
  { id: aboutSections.mission, label: "Mission & vision", mobileLabel: "Mission & vision" },
  { id: aboutSections.values, label: "Core values", mobileLabel: "Core values" },
  { id: aboutSections.founder, label: "Our proprietress", mobileLabel: "Proprietress" },
  { id: aboutSections.why, label: "Why choose us", mobileLabel: "Why us" },
]

export const story = {
  eyebrow: "Our story",
  title: "More than a place of learning. A home away from home.",
  paragraphs: [
    {
      desktop:
        "At Golden Royal Ark School, we believe that every child is unique, gifted, and capable of achieving greatness when given the right guidance, support, and opportunities. Since our inception, we have remained committed to raising academically sound, morally upright, confident, and responsible children who are prepared to excel in an ever-changing world.",
      mobile:
        "At Golden Royal Ark School, we believe that every child is unique, gifted, and capable of achieving greatness when given the right guidance, support, and opportunities.",
    },
    {
      desktop:
        "Our school is a home away from home where children feel loved, valued, and encouraged to discover their full potential. We combine academic excellence with character development, helping our pupils grow intellectually, socially, emotionally, and morally.",
      mobile:
        "We combine academic excellence with character development, helping our pupils grow intellectually, socially, emotionally, and morally.",
    },
  ],
  timeline: [
    {
      year: "2018",
      title: "The beginning",
      text: "Mrs. Fredrick Juliet founds Golden Royal Ark School, beginning with a Day Care and Nursery section.",
      mobileText: "Founded by Mrs. Fredrick Juliet, beginning with a Day Care and Nursery section.",
      icon: "heart",
      tone: "crimson",
    },
    {
      year: "Then",
      title: "Growing with our families",
      text: "Through unwavering dedication, quality teaching and the trust of parents, the school grows steadily and expands to include a Primary School.",
      mobileText:
        "Steady growth through dedication, quality teaching and the trust of parents. A Primary School is added.",
      icon: "graduationCap",
      tone: "royal",
    },
    {
      year: "Today",
      title: "6 months to 12 years",
      text: "Crèche, Preschool and Primary School, providing a strong educational foundation and preparing children for lifelong learning and success.",
      mobileText: "Crèche, Preschool and Primary School for children aged 6 months to 12 years.",
      icon: "bookOpen",
      tone: "gold",
    },
  ] satisfies { year: string; title: string; text: string; mobileText: string; icon: IconName; tone: string }[],
}

export const passion = {
  eyebrow: "What we are passionate about",
  title: "The things that drive us every day.",
  belief: { label: "Our belief", text: "Every child is unique, gifted, and capable of achieving greatness." },
  photo: photos.buildingBlocks,
  items: [
    "Providing a safe, friendly, and welcoming learning environment.",
    "Building strong academic foundations for every child.",
    "Nurturing good character, discipline, and integrity.",
    "Encouraging creativity, critical thinking, and innovation.",
    "Helping children discover and develop their unique talents.",
    "Raising future leaders who will positively impact their communities and the world.",
    "Partnering with parents to ensure the holistic development of every child.",
  ],
}

export const missionVision = {
  eyebrow: "Mission & vision",
  title: "Why we exist, and where we are headed.",
  photo: photos.classroomSmiles,
}

export const valuesIntro = {
  eyebrow: "Our core values",
  titleBefore: "Nine values. Together they spell",
  highlight: "GREATNESS",
  text: "At Golden Royal Ark School, our core values guide everything we do, as we sail into greatness together.",
}

export const founderBio = {
  eyebrow: "Our proprietress",
  stats: [
    { value: "9+ years", label: "in the education sector", mobileLabel: "in education" },
    {
      value: "B.Sc. (Ed.)",
      label: "Agricultural Science, Imo State University",
      mobileLabel: "Agricultural Science, Imo State University",
    },
  ],
  intro:
    "Mrs. Fredrick Juliet is the Founder and Proprietress of Golden Royal Ark School. She holds a Bachelor of Science (B.Sc.) in Education (Agricultural Science) from Imo State University and has over nine years of experience in the education sector.",
  mobileIntro:
    "Mrs. Fredrick Juliet holds a Bachelor of Science (B.Sc.) in Education (Agricultural Science) from Imo State University and has over nine years of experience in the education sector.",
  beforeQuote: [
    "Before establishing Golden Royal Ark School, she taught in several schools, where she developed a deep passion for nurturing young minds and promoting academic excellence. Her years of teaching experience equipped her with the knowledge, skills, and vision to create a learning environment where every child is valued, encouraged, and inspired to succeed.",
    "In 2018, Mrs. Fredrick Juliet founded Golden Royal Ark School, beginning with a Day Care and Nursery section. Through unwavering dedication, quality teaching, and the trust of parents, the school experienced steady growth and was expanded to include a Primary School. Today, Golden Royal Ark School continues to provide a strong educational foundation for children, preparing them for lifelong learning and success.",
  ],
  afterQuote: [
    "Under her leadership, Golden Royal Ark School remains committed to raising confident, responsible, and morally upright learners through dedicated teachers, innovative teaching methods, and a culture of excellence. Her passion for education and unwavering commitment continue to inspire the growth and success of the school and the lives of the children it serves.",
  ],
  /** Phone: the story after the quote folds behind this toggle (FounderStory). */
  mobileMoreLabel: "Read her full story",
}

export const whyAbout = {
  eyebrow: "Why choose us",
  title: "Dedicated to a learning experience where every child thrives.",
  mobileTitle: "Where every child thrives.",
  cta: "Talk to us",
}

export const commitment = {
  eyebrow: "Our commitment",
  title: "Every child matters.",
  text: "We are committed to helping our pupils grow into confident achievers, lifelong learners, and responsible leaders. Through dedication, excellence, and genuine care, we continue to build a solid foundation for the future of every child entrusted to us.",
  mobileText:
    "We are committed to helping our pupils grow into confident achievers, lifelong learners, and responsible leaders.",
  primaryCta: "Send us a message",
  secondaryCta: "Check results",
}
