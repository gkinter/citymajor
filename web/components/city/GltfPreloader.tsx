"use client";

import { useGLTF } from "@react-three/drei";
import { useEffect } from "react";
import { allGltfPaths } from "@/lib/gltf-catalog";

/** Preload catalog GLTF building assets before the canvas mounts. */
export function GltfPreloader() {
  useEffect(() => {
    for (const path of allGltfPaths()) {
      useGLTF.preload(path);
    }
  }, []);

  return null;
}
