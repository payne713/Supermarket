using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Supermarket.DataAccess.Interface;
using Supermarket.DataAccess.Tools;
using Supermarket.Domain.Enums;
using Supermarket.Domain.Models;
using Supermarket.Domain.Request;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;

namespace Supermarket.DataAccess.Token
{
    public class TokenHelper : ITokenHelper
    {
        private readonly IOptions<JWTConfig> _options;
        private readonly IUserService userService;

        public TokenHelper(IOptions<JWTConfig> options,IUserService userService)
        {
            this._options = options;
            this.userService = userService;
        }

        public Token CreateAccessToken(User user)
        {
            var claims = new Claim[] 
            { 
                new Claim(ClaimTypes.NameIdentifier, user.Id), 
                new Claim(ClaimTypes.Name, user.UserName) 
            };

            return CreateToken(claims, TokenType.AccessToken);
        }

        public ComplexToken CreateToken(User user)
        {
            var claims = new Claim[] 
            { 
                 new Claim(ClaimTypes.NameIdentifier, user.Id), 
                    new Claim(ClaimTypes.Name, user.UserName) 
                };

            return CreateToken(claims);
        }

        public ComplexToken CreateToken(Claim[] claims)
        {
            return new ComplexToken 
            { 
                AccessToken = CreateToken(claims, TokenType.AccessToken), 
                RefreshToken = CreateToken(claims, TokenType.RefreshToken) 
            };
        }

        /// <summary>
        /// 用于创建AccessToken和RefreshToken。
        /// 这里AccessToken和RefreshToken只是过期时间不同，【实际项目】中二者的claims内容可能会不同。
        /// 因为RefreshToken只是用于刷新AccessToken，其内容可以简单一些。
        /// 而AccessToken可能会附加一些其他的Claim。
        /// </summary>
        /// <param name="claims"></param>
        /// <param name="tokenType"></param>
        /// <returns></returns>
        private Token CreateToken(Claim[] claims, TokenType tokenType)
        {
            var now = DateTime.Now;
            var expires = now.Add(TimeSpan.FromMinutes(
                            tokenType.Equals(TokenType.AccessToken)  
                            ? _options.Value.AccessTokenExpiresMinutes 
                            : _options.Value.RefreshTokenExpiresMinutes)
                        );
            var token = new JwtSecurityToken(
                issuer: _options.Value.Issuer,
                audience: tokenType.Equals(TokenType.AccessToken)
                 ? _options.Value.Audience 
                 : _options.Value.RefreshTokenAudience,
                claims: claims,
                notBefore: now,
                expires: expires,
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Value.IssuerSigningKey)), SecurityAlgorithms.HmacSha256));
            return new Token 
            { 
                TokenContent = new JwtSecurityTokenHandler().WriteToken(token), 
                Expires = expires 
            };
        }

        public Token RefreshToken(ClaimsPrincipal claimsPrincipal)
        {
            var code = claimsPrincipal.Claims.FirstOrDefault(m => m.Type.Equals(ClaimTypes.NameIdentifier));
            if (null != code)
            {
                return CreateAccessToken(userService.GetUser(code.Value.ToString()));
            }
            else
            {
                return null;
            }
        }
    }
}