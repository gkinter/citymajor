"use client";

import { Component, type ErrorInfo, type ReactNode } from "react";
import { markGltfCatalogFailed } from "@/lib/gltf-load-state";

type GltfBucketBoundaryProps = {
  catalogKey: string;
  children: ReactNode;
};

type GltfBucketBoundaryState = {
  failed: boolean;
};

/**
 * Catches GLTF parse/load failures for one catalog bucket so box fallbacks stay visible.
 */
export class GltfBucketBoundary extends Component<
  GltfBucketBoundaryProps,
  GltfBucketBoundaryState
> {
  state: GltfBucketBoundaryState = { failed: false };

  static getDerivedStateFromError(): GltfBucketBoundaryState {
    return { failed: true };
  }

  componentDidCatch(error: Error, info: ErrorInfo): void {
    console.warn(
      `[CityMajor] GLTF bucket failed (${this.props.catalogKey}) — using box fallback`,
      error,
      info.componentStack,
    );
    markGltfCatalogFailed(this.props.catalogKey);
  }

  render(): ReactNode {
    if (this.state.failed) return null;
    return this.props.children;
  }
}
