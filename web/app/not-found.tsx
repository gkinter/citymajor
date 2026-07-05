import Link from "next/link";

export default function NotFound() {
  return (
    <div className="hud-not-found">
      <div className="hud-not-found__glow" aria-hidden />
      <main className="hud-not-found__panel">
        <p className="hud-not-found__eyebrow">CityMajor Web</p>
        <h1 className="hud-not-found__code">404</h1>
        <p className="hud-not-found__title">Sector not mapped</p>
        <p className="hud-not-found__body">
          This route isn&apos;t on the city grid. Return to the mayor&apos;s desk and keep
          building.
        </p>
        <Link href="/play" className="hud-not-found__cta">
          Back to /play
        </Link>
      </main>
    </div>
  );
}
