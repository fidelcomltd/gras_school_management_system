import { photos, type Photo } from "@/content/images"

/** Home page copy, verbatim from the "Home — Desktop" and "Home — Mobile" artboards. */

export type HeroSlide = {
  photo: Photo
  /** CSS object-position, desktop then phone (the phone crop is portrait). */
  position: { desktop: string; mobile: string }
  eyebrow: string
  title: string
  text: string
}

export const heroSlides: HeroSlide[] = [
  {
    photo: photos.morningAssembly,
    position: { desktop: "center 45%", mobile: "center 40%" },
    eyebrow: "Crèche · Preschool · Primary",
    title: "Building future leaders through excellence, character and care.",
    text: "A nurturing, child-centred school where every child is valued, encouraged and inspired to succeed.",
  },
  {
    photo: photos.classroomSmiles,
    position: { desktop: "center 62%", mobile: "center 55%" },
    eyebrow: "A home away from home",
    title: "Where every child feels loved, valued and encouraged.",
    text: "We combine academic excellence with character development, so our pupils grow intellectually, socially, emotionally and morally.",
  },
  {
    photo: photos.outdoorBlocks,
    position: { desktop: "center 40%", mobile: "40% center" },
    eyebrow: "Ages 6 months – 12 years",
    title: "Every child is unique, gifted and capable of greatness.",
    text: "Early learning through play and creativity, built on strong academic foundations.",
  },
  {
    photo: photos.schoolCelebration,
    position: { desktop: "center 55%", mobile: "center 50%" },
    eyebrow: "Sailing into greatness",
    title: "Raising confident, responsible and morally upright learners.",
    text: "Dedicated teachers, innovative teaching methods and a culture of excellence.",
  },
]

export type Stat = { value: string; label: string; mobileLabel: string; tone: "crimson" | "royal" | "gold" | "azure" }

export const stats: Stat[] = [
  {
    value: "2018",
    label: "Founded, starting with Day Care & Nursery",
    mobileLabel: "Founded, starting with Day Care & Nursery",
    tone: "crimson",
  },
  {
    value: "6m – 12y",
    label: "Care and education from 6 months to 12 years",
    mobileLabel: "Care and education by age",
    tone: "royal",
  },
  {
    value: "3 sections",
    label: "Crèche, Preschool and Primary School",
    mobileLabel: "Crèche, Preschool, Primary",
    tone: "gold",
  },
  {
    value: "Low ratio",
    label: "A low pupil-to-teacher ratio in every class",
    mobileLabel: "Pupil-to-teacher, in every class",
    tone: "azure",
  },
]

export const welcome = {
  eyebrow: "Welcome to Golden Royal Ark School",
  mobileEyebrow: "Welcome",
  title: "A nurturing, child-centred learning community.",
  paragraphs: [
    {
      desktop:
        "Established in 2018 with a vision to provide quality education in a safe, caring and inspiring environment. We believe that every child is unique, gifted and capable of achieving greatness when given the right guidance, support and opportunities.",
      mobile:
        "Established in 2018 with a vision to provide quality education in a safe, caring and inspiring environment. We believe that every child is unique, gifted and capable of achieving greatness.",
    },
    {
      desktop:
        "Our school is more than a place of learning. It is a home away from home, where children feel loved, valued and encouraged to discover their full potential.",
      mobile: "Our school is a home away from home, where children feel loved, valued and encouraged.",
    },
  ],
  qualities: ["Academically sound", "Morally upright", "Confident", "Responsible"],
  badge: { title: "Every child matters", subtitle: "A home away from home" },
  photos: { main: photos.welcomeMural, inset: photos.readingBook },
}

export const servicesIntro = {
  eyebrow: "Our services",
  title: "Quality care and education from 6 months to 12 years.",
  mobileTitle: "Care and education from 6 months to 12 years.",
  text: "At Golden Royal Ark School, we nurture every child to learn, grow, and succeed.",
  swipeHint: "Swipe to see all three",
}

export const whyIntro = {
  eyebrow: "Why choose us",
  title: "A learning experience where every child thrives.",
  text: "We combine academic excellence with character development, helping our pupils grow intellectually, socially, emotionally and morally.",
  note: "Partnering with parents to ensure the holistic development of every child.",
}

export type GalleryItem = { photo: Photo; alt: string; width: number; height: number; tilt: number }

/** Marquee cards: desktop size in px; the phone renders them at two-thirds. */
export const gallery = {
  eyebrow: "Gallery",
  title: "Life at Golden Royal Ark",
  text: "Moments from our classrooms, our playground and our celebrations.",
  items: [
    { photo: photos.bandMarch, alt: "The school band leading a march", width: 300, height: 400, tilt: -1.5 },
    { photo: photos.lawnPlay, alt: "Children playing with toys on the lawn", width: 420, height: 300, tilt: 1 },
    { photo: photos.readingBook, alt: "A pupil holding her reading book", width: 270, height: 360, tilt: -0.5 },
    { photo: photos.schoolParty, alt: "Pupils dressed up for a school party", width: 300, height: 420, tilt: 1.5 },
    { photo: photos.toddlersLawn, alt: "Toddlers on the playground", width: 290, height: 340, tilt: -1 },
    { photo: photos.buildingBlocks, alt: "Children building with blocks together", width: 320, height: 400, tilt: 0.5 },
    {
      photo: photos.welcomeMural,
      alt: "The welcome mural at the school entrance",
      width: 260,
      height: 320,
      tilt: -1.5,
    },
    { photo: photos.canopyCelebration, alt: "A celebration under the school canopy", width: 300, height: 380, tilt: 1 },
    { photo: photos.playground, alt: "The school playground and painted walls", width: 280, height: 360, tilt: -0.5 },
    { photo: photos.morningAssembly, alt: "Morning assembly on the school lawn", width: 300, height: 410, tilt: 1.2 },
    { photo: photos.schoolGrounds, alt: "The school grounds", width: 270, height: 330, tilt: -1 },
  ] satisfies GalleryItem[],
}

export const founderTeaser = {
  eyebrow: "Meet our founder",
  experience: { value: "9+ years", label: "in the education sector" },
  cta: "Read her story",
}

export const resultBand = {
  badge: "For parents",
  title: "Check your child's result online.",
  text: "Use your child's registration number and access PIN on our secure result portal.",
  mobileText: "Use your child's registration number and access PIN on our secure result portal. No account needed.",
  cta: "Go to the Result Portal",
  quickCard: { title: "Check your child's result", subtitle: "Registration number + access PIN" },
  steps: [
    { title: "Open the result portal", detail: "From any phone or computer" },
    { title: "Enter the registration number and access PIN", detail: "Keep both handy before you start" },
    { title: "View your child's published result", detail: "No account or sign-up needed" },
  ],
}

export const homeCta = {
  title: "Give your child a solid foundation for the future.",
  text: "Talk to us about our Crèche, Preschool and Primary School.",
  cta: "Send us a message",
}
