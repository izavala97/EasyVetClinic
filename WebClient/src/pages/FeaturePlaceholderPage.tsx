import { Construction } from 'lucide-react'

type FeaturePlaceholderPageProps = {
  eyebrow: string
  title: string
}

export function FeaturePlaceholderPage({ eyebrow, title }: FeaturePlaceholderPageProps) {
  return (
    <section className="panel feature-placeholder">
      <Construction size={24} />
      <p className="eyebrow">{eyebrow}</p>
      <h1>{title}</h1>
      <p>This workspace will be available in a future iteration.</p>
    </section>
  )
}