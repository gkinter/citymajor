"use client";

import { CHUNKS_PER_AXIS } from "@/lib/constants";

type MinimapSceneProps = {
  chunkColors: string[];
};

/** One colored plane per chunk in an 8×8 orthographic grid. */
export function MinimapScene({ chunkColors }: MinimapSceneProps) {
  return (
    <>
      <ambientLight intensity={1} />
      {chunkColors.map((color, index) => {
        const cx = index % CHUNKS_PER_AXIS;
        const cz = Math.floor(index / CHUNKS_PER_AXIS);
        return (
          <mesh
            key={index}
            position={[cx + 0.5, 0, cz + 0.5]}
            rotation={[-Math.PI / 2, 0, 0]}
          >
            <planeGeometry args={[0.92, 0.92]} />
            <meshBasicMaterial color={color} />
          </mesh>
        );
      })}
    </>
  );
}
