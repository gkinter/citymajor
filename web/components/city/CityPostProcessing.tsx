"use client";

import { Bloom, EffectComposer } from "@react-three/postprocessing";
import { useFrame } from "@react-three/fiber";
import { useState } from "react";
import { UnsignedByteType } from "three";
import type { GraphicsQualityTier } from "@/lib/constants";

type CityPostProcessingProps = {
  qualityTier: GraphicsQualityTier;
};

/**
 * Tiered post-processing — bloom on high quality only.
 * Defers composer mount until after the first scene frame and uses UnsignedByteType
 * render targets for broader GPU compatibility (HalfFloat often yields a black canvas).
 */
export function CityPostProcessing({ qualityTier }: CityPostProcessingProps) {
  const [ready, setReady] = useState(false);

  useFrame(() => {
    if (!ready) setReady(true);
  }, 1);

  if (qualityTier !== "high" || !ready) return null;

  return (
    <EffectComposer
      enableNormalPass={false}
      multisampling={0}
      frameBufferType={UnsignedByteType}
    >
      <Bloom
        luminanceThreshold={0.65}
        luminanceSmoothing={0.4}
        intensity={0.35}
        mipmapBlur
      />
    </EffectComposer>
  );
}
