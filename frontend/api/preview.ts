import { previewResponse } from '../src/share/previewHandler.js'

// Reached only through the bot rewrite in vercel.json; see previewHandler.ts.
export function GET(request: Request): Promise<Response> {
  return previewResponse(
    request,
    process.env.VITE_API_BASE_URL ?? 'https://cricket-live-api-qwo6.onrender.com',
  )
}
