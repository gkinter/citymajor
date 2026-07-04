import allTechnologies from "../../base/data/tech/technologies.json";

export type TechPreview = {
  id: string;
  name: string;
  era: string;
  category: string;
  cost_rp: number;
  prerequisites: string[];
  description: string;
};

const catalog = allTechnologies as TechPreview[];

/** Full catalog size (154 technologies in design). */
export const TECH_CATALOG_TOTAL = catalog.length;

/** First 20 technologies for Research UI v1 stub. */
export const TECH_PREVIEW_LIST: TechPreview[] = catalog.slice(0, 20);
