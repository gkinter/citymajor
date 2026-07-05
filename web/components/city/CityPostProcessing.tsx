"use client";

import { Bloom, EffectComposer } from "@react-three/postprocessing";
import { useFrame } from "@react-three/fiber";
import { useEffect, useState } from "react";
import { UnsignedByteType } from "three";
import type { GraphicsQualityTier } from "@/lib/constants";

type CityPostProcessingProps = {
  qualityTier: GraphicsQualityTier;
  /** Set when CanvasRenderHealth detects a black-frame streak from EffectComposer. */
  disabled?: boolean;
  /** Defer bloom until the city scene reports visible geometry (avoids black RT on slow prod). */
  sceneReady?: boolean;
};

/**
 * Tiered post-processing — bloom on high quality only.
 * Defers composer mount until after the first scene frame and uses UnsignedByteType
 * render targets for broader GPU compatibility (HalfFloat often yields a black canvas).
 */
export function CityPostProcessing({
  qualityTier,
  disabled = false,
  sceneReady = false,
}: CityPostProcessingProps) {
  const [ready, setReady] = useState(false);
  const [stableFrames, setStableFrames] = useState(0);

  useEffect(() => {
    setStableFrames(0);
  }, [sceneReady]);

  useFrame(() => {
    if (!ready) {
      setReady(true);
      return;
    }
    if (sceneReady && stableFrames < 3) {
      setStableFrames((n) => n + 1);
    }
  }, 1);

  if (
    disabled ||
    qualityTier !== "high" ||
    !ready ||
    !sceneReady ||
    stableFrames < 3
  ) {
    return null;
  }

  return (
    <EffectComposer
      enableNormalPass={false}
      multisampling={0}
      frameBufferType={UnsignedByteType}
      autoClear
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
