"use client";

import { Bloom, EffectComposer } from "@react-three/postprocessing";
import type { GraphicsQualityTier } from "@/lib/constants";

type CityPostProcessingProps = {
  qualityTier: GraphicsQualityTier;
};

/** Tiered post-processing — bloom enabled on high quality only. */
export function CityPostProcessing({ qualityTier }: CityPostProcessingProps) {
  if (qualityTier !== "high") return null;

  return (
    <EffectComposer enableNormalPass={false} multisampling={0}>
      <Bloom
        luminanceThreshold={0.65}
        luminanceSmoothing={0.4}
        intensity={0.35}
        mipmapBlur
      />
    </EffectComposer>
  );
}
