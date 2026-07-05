import { z } from "zod";

export const SaveSummarySchema = z.object({
  tick: z.number().int().nonnegative(),
  population: z.number().int().nonnegative(),
  cityFunds: z.number(),
  era: z.number().int().min(0).max(4),
});
export type SaveSummary = z.infer<typeof SaveSummarySchema>;

export const SaveSlotSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1).max(64),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  revision: z.number().int().positive().default(1),
  formatVersion: z.number().int().nonnegative().default(0),
  snapshotSchemaVersion: z.number().int().positive().default(1),
  summary: SaveSummarySchema,
  payload: z.record(z.string(), z.unknown()).optional(),
  blobKey: z.string().optional(),
  blobBytes: z.number().int().nonnegative().optional(),
  thumbnailKey: z.string().optional(),
  wasmBlobBase64: z.string().optional(),
});
export type SaveSlot = z.infer<typeof SaveSlotSchema>;

export const SaveSlotListItemSchema = SaveSlotSchema.omit({ payload: true });
export type SaveSlotListItem = z.infer<typeof SaveSlotListItemSchema>;

export const SaveListResponseSchema = z.object({
  saves: z.array(SaveSlotListItemSchema),
  count: z.number().int().nonnegative(),
  maxSlots: z.number().int().positive(),
  tier: z.enum(["free", "founder_pass"]),
  storageBackend: z.enum(["local", "r2"]),
});
export type SaveListResponse = z.infer<typeof SaveListResponseSchema>;

export const CreateSaveResponseSchema = z.object({
  save: SaveSlotSchema,
  count: z.number().int().nonnegative(),
  maxSlots: z.number().int().positive(),
  tier: z.enum(["free", "founder_pass"]),
});
export type CreateSaveResponse = z.infer<typeof CreateSaveResponseSchema>;

export const GetSaveResponseSchema = z.object({
  save: SaveSlotSchema,
  downloadUrl: z.string().url().nullable(),
});
export type GetSaveResponse = z.infer<typeof GetSaveResponseSchema>;
