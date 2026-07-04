import { PlayClient } from "@/components/city/PlayClient";
import { GltfPreloader } from "@/components/city/GltfPreloader";

export default function PlayPage() {
  return (
    <>
      <GltfPreloader />
      <PlayClient />
    </>
  );
}
