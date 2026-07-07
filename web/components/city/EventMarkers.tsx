"use client";

import { Billboard, Html } from "@react-three/drei";
import { useFrame } from "@react-three/fiber";
import { useMemo, useRef, type CSSProperties } from "react";
import * as THREE from "three";
import {
  getEventMarkerLabel,
  getEventMarkerStyle,
} from "@/lib/event-catalog";
import type { ActiveEventSnapshot } from "@/lib/sim-bridge";

type EventMarkersProps = {
  activeEvents?: ActiveEventSnapshot[];
  onEventClick?: (event: ActiveEventSnapshot) => void;
};

type PlacedEvent = ActiveEventSnapshot & { tileZ: number };

function isPlacedEvent(event: ActiveEventSnapshot): event is PlacedEvent {
  return (
    event.phase === "active" &&
    event.tileX >= 0 &&
    event.tileY >= 0
  );
}

function EventMarker({
  event,
  onClick,
}: {
  event: PlacedEvent;
  onClick?: (event: ActiveEventSnapshot) => void;
}) {
  const ringRef = useRef<THREE.Mesh>(null);
  const style = useMemo(() => getEventMarkerStyle(event.typeId), [event.typeId]);
  const label = useMemo(() => getEventMarkerLabel(event.typeId), [event.typeId]);

  useFrame(({ clock }) => {
    const ring = ringRef.current;
    if (!ring) return;
    const pulse = 1 + 0.18 * Math.sin(clock.elapsedTime * 3.2 + event.eventId);
    ring.scale.set(pulse, pulse, pulse);
    const material = ring.material as THREE.MeshBasicMaterial;
    material.opacity = 0.35 + 0.2 * Math.sin(clock.elapsedTime * 3.2 + event.eventId);
  });

  return (
    <group position={[event.tileX + 0.5, 2.8, event.tileZ + 0.5]}>
      <mesh ref={ringRef} rotation={[-Math.PI / 2, 0, 0]}>
        <ringGeometry args={[0.55, 0.78, 32]} />
        <meshBasicMaterial
          color={style.color}
          transparent
          opacity={0.5}
          depthWrite={false}
          side={THREE.DoubleSide}
        />
      </mesh>
      <mesh rotation={[-Math.PI / 2, 0, 0]} position={[0, -2.75, 0]}>
        <circleGeometry args={[0.22, 24]} />
        <meshBasicMaterial color={style.color} transparent opacity={0.25} />
      </mesh>
      <Billboard>
        <Html center distanceFactor={72} zIndexRange={[40, 0]}>
          <button
            type="button"
            className="event-marker"
            style={{ "--event-marker-color": style.color } as CSSProperties}
            title={label}
            aria-label={`${label} — open Herald`}
            onClick={(e) => {
              e.stopPropagation();
              onClick?.(event);
            }}
          >
            <span className="event-marker__glyph" aria-hidden>
              {style.glyph}
            </span>
          </button>
        </Html>
      </Billboard>
    </group>
  );
}

/** Pulsing map markers for live WASM events; click opens Herald for that event. */
export function EventMarkers({ activeEvents, onEventClick }: EventMarkersProps) {
  const placed = useMemo(
    () => (activeEvents ?? []).filter(isPlacedEvent).map((e) => ({ ...e, tileZ: e.tileY })),
    [activeEvents],
  );

  if (placed.length === 0) return null;

  return (
    <group name="event-markers">
      {placed.map((event) => (
        <EventMarker
          key={event.eventId}
          event={event}
          onClick={onEventClick}
        />
      ))}
    </group>
  );
}
