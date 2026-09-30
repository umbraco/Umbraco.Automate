const umbracoConfig = {
  environment: {
    // The demo site binds a dynamic port, so there is no sensible default here. `npm run config`
    // resolves the live address from the demo site's named pipe and writes it into .env.
    baseUrl: process.env.URL || 'https://localhost:44380'
  },
  user: {
    login: process.env.UMBRACO_USER_LOGIN,
    password: process.env.UMBRACO_USER_PASSWORD
  }
};

export { umbracoConfig };
