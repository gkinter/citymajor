import { z } from "zod";

export const SaveSlotSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1).max(64),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  payload: z.record(z.string(), z.unknown()).default({}),
});
export type SaveSlot = z.infer<typeof SaveSlotSchema>;

export const SaveListResponseSchema = z.object({
  saves: z.array(SaveSlotSchema),
  count: z.number().int().nonnegative(),
  maxSlots: z.number().int().positive(),
  tier: z.enum(["free", "founder_pass"]),
});
export type SaveListResponse = z.infer<typeof SaveListResponseSchema>;

export const CreateSaveResponseSchema = z.object({
  save: SaveSlotSchema,
  count: z.number().int().nonnegative(),
  maxSlots: z.number().int().positive(),
  tier: z.enum(["free", "founder_pass"]),
});
export type CreateSaveResponse = z.infer<typeof CreateSaveResponseSchema>;
