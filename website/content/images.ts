import type { StaticImageData } from "next/image"
import bandMarch from "@/assets/photos/band-march.jpg"
import buildingBlocks from "@/assets/photos/building-blocks.jpg"
import canopyCelebration from "@/assets/photos/canopy-celebration.jpg"
import classroomSmiles from "@/assets/photos/classroom-smiles.jpg"
import lawnPlay from "@/assets/photos/lawn-play.jpg"
import morningAssembly from "@/assets/photos/morning-assembly.jpg"
import outdoorBlocks from "@/assets/photos/outdoor-blocks.jpg"
import playground from "@/assets/photos/playground.jpg"
import preschoolBlocks from "@/assets/photos/preschool-blocks.jpg"
import proprietress from "@/assets/photos/proprietress.jpg"
import readingBook from "@/assets/photos/reading-book.jpg"
import schoolCelebration from "@/assets/photos/school-celebration.jpg"
import schoolGrounds from "@/assets/photos/school-grounds.jpg"
import schoolParty from "@/assets/photos/school-party.jpg"
import toddlersLawn from "@/assets/photos/toddlers-lawn.jpg"
import welcomeMural from "@/assets/photos/welcome-mural.jpg"

/**
 * Every photo on the site. Static imports give next/image the intrinsic size (no layout shift) and a
 * blur placeholder for free. Masters are produced by `npm run images` (scripts/process-images.mjs).
 */
export type Photo = { src: StaticImageData; alt: string }

export const photos = {
  bandMarch: { src: bandMarch, alt: "Primary pupils marching with the school band" },
  buildingBlocks: { src: buildingBlocks, alt: "Children building with colourful blocks together" },
  canopyCelebration: { src: canopyCelebration, alt: "A celebration under the school canopy" },
  classroomSmiles: { src: classroomSmiles, alt: "A smiling pupil at a classroom table with friends" },
  lawnPlay: { src: lawnPlay, alt: "Children playing with toys on the lawn" },
  morningAssembly: { src: morningAssembly, alt: "Pupils in uniform lined up for morning assembly" },
  outdoorBlocks: { src: outdoorBlocks, alt: "Children in orange uniforms playing with building blocks outdoors" },
  playground: { src: playground, alt: "The school playground and painted walls" },
  preschoolBlocks: { src: preschoolBlocks, alt: "Preschoolers building with colourful blocks" },
  proprietress: { src: proprietress, alt: "Mrs. Fredrick Juliet, Founder and Proprietress of Golden Royal Ark School" },
  readingBook: { src: readingBook, alt: "A young pupil proudly holding up her reading book" },
  schoolCelebration: { src: schoolCelebration, alt: "Pupils and teachers together at a school celebration" },
  schoolGrounds: { src: schoolGrounds, alt: "The school grounds" },
  schoolParty: { src: schoolParty, alt: "Pupils dressed up for a school party" },
  toddlersLawn: { src: toddlersLawn, alt: "Toddlers playing together on the school lawn" },
  welcomeMural: { src: welcomeMural, alt: "A hand-painted welcome mural on a classroom wall" },
} satisfies Record<string, Photo>
