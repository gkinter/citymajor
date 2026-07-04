import { hudEraName } from "@/lib/era";
import type { NarrativeEventResponse } from "@/lib/narrative-templates";

export type EraTransitionModalCopy = {
  eyebrow: string;
  title: string;
  body: string;
};

const MODAL_COPY: Record<number, EraTransitionModalCopy> = {
  1: {
    eyebrow: "Era transition",
    title: "The Industrial Age begins",
    body:
      "Smokestacks pierce the skyline and freight yards hum with steel. Your frontier settlement has become a city of industry — factories, unions, and ambition.",
  },
  2: {
    eyebrow: "Era transition",
    title: "Postwar prosperity arrives",
    body:
      "Suburbs spread beyond the old core. Automobiles crowd new boulevards and a generation expects comfort, convenience, and room to grow.",
  },
  3: {
    eyebrow: "Era transition",
    title: "The Modern era dawns",
    body:
      "Glass towers replace brick blocks. Global finance, digital networks, and dense urban living define what your city can become next.",
  },
  4: {
    eyebrow: "Era transition",
    title: "Welcome to the Future",
    body:
      "Arcologies rise above the grid. Automation, clean energy, and post-scarcity dreams push your metropolis beyond anything prior generations imagined.",
  },
};

const HERALD_COPY: Record<
  number,
  Pick<NarrativeEventResponse, "headline" | "body">
> = {
  1: {
    headline: "SPECIAL EDITION — Smoke and Steel: A City Forged",
    body:
      "Editor's note: With the last whistle of the frontier rail fading, mill owners toast record output while union halls fill with new members. The Herald dedicates this edition to the workers, investors, and families who dragged a camp into the Industrial Age. Robber barons flourish, yes — but so does opportunity.",
  },
  2: {
    headline: "SPECIAL EDITION — Wheels, Wires, and the American Dream",
    body:
      "Television antennas sprout on every roof and the automobile reshapes daily life. Veterans return to expanding suburbs; downtown merchants chase the flight to convenience. The Herald marks this Postwar chapter with hope — and a warning that sprawl has a price.",
  },
  3: {
    headline: "SPECIAL EDITION — Connected, Crowded, Changing",
    body:
      "Fiber and finance now move faster than traffic. Gentrification debates echo in council chambers while tech campuses redraw the skyline. The Herald's Modern-era supplement chronicles a city racing forward — anxious, ambitious, and alive.",
  },
  4: {
    headline: "SPECIAL EDITION — Tomorrow, Built Today",
    body:
      "Fusion pilots, orbital cargo manifests, and universal-basic-income hearings share the front page. Your city has entered the Future — a place where abundance and inequality argue in the same sentence. The Herald will be watching what you build next.",
  },
};

export function eraTransitionModalCopy(era: number): EraTransitionModalCopy {
  const eraName = hudEraName(era);
  return (
    MODAL_COPY[era] ?? {
      eyebrow: "Era transition",
      title: `${eraName} era unlocked`,
      body: `Your city has advanced into the ${eraName} era. New technologies, buildings, and challenges await.`,
    }
  );
}

/** Herald special-edition story when the sim era increments. */
export function narrativeFromEraTransition(era: number): NarrativeEventResponse {
  const eraName = hudEraName(era);
  const copy = HERALD_COPY[era] ?? {
    headline: `SPECIAL EDITION — Entering the ${eraName} Era`,
    body: `Citizens gather in the square as your city officially crosses into the ${eraName} age. The Herald marks the milestone with a commemorative issue celebrating how far you have come — and how much remains to build.`,
  };

  return {
    bucket: "prosperity_high",
    headline: copy.headline,
    body: copy.body,
    options: [],
    source: "template",
  };
}
