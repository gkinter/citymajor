import { ShopClient } from "@/components/shop/ShopClient";
import { isStripeCheckoutEnabled } from "@/lib/stripe";

export const metadata = {
  title: "Shop — CityMajor",
  description: "Founder Pass and cosmetic shop",
};

export default function ShopPage() {
  return <ShopClient stripeCheckoutEnabled={isStripeCheckoutEnabled()} />;
}
