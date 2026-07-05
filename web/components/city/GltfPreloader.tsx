"use client";

import { useGLTF } from "@react-three/drei";
import { useEffect } from "react";
import { allGltfPaths } from "@/lib/gltf-catalog";

/** Preload catalog GLTF building assets before the canvas mounts. */
export function GltfPreloader() {
  useEffect(() => {
    for (const path of allGltfPaths()) {
      try {
        useGLTF.preload(path);
      } catch (err) {
        console.warn(`[CityMajor] GLTF preload skipped (${path})`, err);
      }
    }
  }, []);

  return null;
}
