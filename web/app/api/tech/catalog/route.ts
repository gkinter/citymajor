import { NextResponse } from "next/server";
import {
  TECH_CATALOG_TOTAL,
  TECH_PREVIEW_LIST,
  type TechPreview,
} from "@/lib/tech-catalog";

export type TechCatalogResponse = {
  total: number;
  previewCount: number;
  technologies: TechPreview[];
};

/** Stub API — returns first 20 technologies from the bundled catalog. */
export async function GET() {
  const body: TechCatalogResponse = {
    total: TECH_CATALOG_TOTAL,
    previewCount: TECH_PREVIEW_LIST.length,
    technologies: TECH_PREVIEW_LIST,
  };
  return NextResponse.json(body);
}
