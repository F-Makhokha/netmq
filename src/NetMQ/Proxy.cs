﻿using System;
using JetBrains.Annotations;
using NetMQ.zmq;

namespace NetMQ
{
    /// <summary>
    /// Forward messages between two sockets, you can also specify control socket which both sockets will send messages to
    /// </summary>
    public class Proxy
    {
        [NotNull] private readonly NetMQSocket m_frontend;
        [NotNull] private readonly NetMQSocket m_backend;
        [CanBeNull] private readonly NetMQSocket m_control;
        private Poller m_poller;

        public Proxy([NotNull] NetMQSocket frontend, [NotNull] NetMQSocket backend, [CanBeNull] NetMQSocket control)
        {
            m_frontend = frontend;
            m_backend = backend;
            m_control = control;
        }

        /// <summary>
        /// Start the proxy work, this will block until one of the sockets is closed
        /// </summary>
        /// <exception cref="InvalidOperationException">The proxy has already been started.</exception>
        public void Start()
        {
            if (m_poller != null)
            {
                throw new InvalidOperationException("Proxy has already been started");
            }

            m_frontend.ReceiveReady += OnFrontendReady;
            m_backend.ReceiveReady += OnBackendReady;

            m_poller = new Poller(m_frontend, m_backend);
            m_poller.PollTillCancelled();
        }

        /// <summary>
        /// Stops the proxy, blocking until the underlying <see cref="Poller"/> has completed.
        /// </summary>
        /// <exception cref="InvalidOperationException">The proxy has not been started.</exception>
        public void Stop()
        {
            if (m_poller == null)
            {
                throw new InvalidOperationException("Proxy has not been started");
            }

            m_poller.CancelAndJoin();
            m_poller = null;
        }

        private void OnFrontendReady(object sender, NetMQSocketEventArgs e)
        {
            ProxyBetween(m_frontend, m_backend, m_control);
        }

        private void OnBackendReady(object sender, NetMQSocketEventArgs e)
        {
            ProxyBetween(m_backend, m_frontend, m_control);
        }

        private static void ProxyBetween(NetMQSocket from, NetMQSocket to, [CanBeNull] NetMQSocket control)
        {
            Msg msg = new Msg();
            msg.InitEmpty();

            Msg copy = new Msg();
            copy.InitEmpty();

            while (true)
            {
                from.Receive(ref msg, SendReceiveOptions.None);
                bool more = from.Options.ReceiveMore;

                if (control != null)
                {
                    copy.Copy(ref msg);

                    control.Send(ref copy, more ? SendReceiveOptions.SendMore : SendReceiveOptions.None);
                }

                to.Send(ref msg, more ? SendReceiveOptions.SendMore : SendReceiveOptions.None);

                if (!more)
                {
                    break;
                }
            }

            copy.Close();
            msg.Close();
        }
    }
}
