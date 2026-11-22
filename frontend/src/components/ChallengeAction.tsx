import { useState } from "react";
import { ArrowUpRight } from "lucide-react";
import { request } from "../services/api";
import { ErrorNotice } from "./Feedback";

export function ChallengeAction({
  ids,
  token,
  companyId,
}: {
  ids: string[];
  token: string;
  companyId: string;
}) {
  const [active, setActive] = useState("");
  const [error, setError] = useState<unknown>();
  const [message, setMessage] = useState("");
  const [result, setResult] = useState("");
  async function execute(id: string) {
    setActive(id);
    setError(undefined);
    setMessage("");
    setResult("");
    try {
      const response = await request<unknown>(
        `/challenges/${id}/execute`,
        token,
        companyId,
        { method: "POST", body: "{}" },
      );
      setResult(JSON.stringify(response, null, 2));
      setMessage(
        `${id}: resultado real devuelto por la API con datos de demostración. Esto no acredita pruebas académicas.`,
      );
    } catch (err) {
      setError(err);
    } finally {
      setActive("");
    }
  }
  return (
    <section className="challenge-strip">
      <div>
        <span className="eyebrow">Capacidades académicas</span>
        <p>
          Ejecuta el desafío con datos de demostración y muestra el resultado
          que devuelve la API.
        </p>
      </div>
      <div className="button-row">
        {ids.map((id) => (
          <button
            key={id}
            className="button ghost"
            onClick={() => execute(id)}
            disabled={!!active}
          >
            {active === id ? "Consultando…" : id}
            <ArrowUpRight size={15} />
          </button>
        ))}
      </div>
      <ErrorNotice error={error} />
      {message && <p role="status">{message}</p>}
      {result && <pre className="json-result" aria-label="Resultado devuelto por la API">{result}</pre>}
    </section>
  );
}
