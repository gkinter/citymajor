import { NextResponse } from "next/server";
import {
  TECH_CATALOG_TOTAL,
  TECH_V1_CATALOG,
  type TechPreview,
} from "@/lib/tech-catalog";

export type TechCatalogResponse = {
  total: number;
  technologies: TechPreview[];
};

/** Returns WEB v1 playable tech catalog (Frontier + Industrial eras only). */
export async function GET() {
  const body: TechCatalogResponse = {
    total: TECH_CATALOG_TOTAL,
    technologies: TECH_V1_CATALOG,
  };
  return NextResponse.json(body);
}
